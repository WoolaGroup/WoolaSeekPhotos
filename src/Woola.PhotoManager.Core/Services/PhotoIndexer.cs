using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Core.Agents;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

public class PhotoIndexer : IPhotoIndexer
{
    private readonly PhotoRepository        _photoRepository;
    private readonly TagRepository          _tagRepository;
    private readonly IThumbnailService      _thumbnailService;
    private readonly IMetadataService       _metadataService;
    private readonly IAgentOrchestrator     _agentOrchestrator;
    private readonly ILogger<PhotoIndexer>  _logger;

    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;

    public PhotoIndexer(
        PhotoRepository        photoRepository,
        TagRepository          tagRepository,
        IThumbnailService      thumbnailService,
        IMetadataService       metadataService,
        IAgentOrchestrator     agentOrchestrator,
        ILogger<PhotoIndexer>  logger)
    {
        _photoRepository   = photoRepository;
        _tagRepository     = tagRepository;
        _thumbnailService  = thumbnailService;
        _metadataService   = metadataService;
        _agentOrchestrator = agentOrchestrator;
        _logger            = logger;
    }

    private static readonly HashSet<string> SupportedExtensions = new(
        new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" },
        StringComparer.OrdinalIgnoreCase);

    public event EventHandler<IndexingProgress>? ProgressChanged;
    public bool IsRunning => _isRunning;

    public async Task StartIndexingAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        if (_isRunning) return;
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Directory not found: {rootPath}");

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        try
        {
            await IndexDirectoryAsync(rootPath, _cancellationTokenSource.Token);
        }
        finally
        {
            _isRunning = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    public Task StopIndexingAsync()
    {
        _cancellationTokenSource?.Cancel();
        return Task.CompletedTask;
    }

    // IMP-T3-005: Streaming discovery + parallel hashing via Channel pipeline
    private async Task IndexDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Indexer] Iniciando streaming en '{Dir}'", directory);

        // Progreso inicial inmediato, antes de encontrar el primer archivo
        var progress = new IndexingProgress { TotalFound = 0, Processed = 0, CurrentFile = "Descubriendo archivos..." };
        OnProgressChanged(progress);

        // EnumerateFiles: streaming — no bloquea hasta listar todo
        var fileStream = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));

        // Canal productor→consumidor con backpressure (max 200 en vuelo)
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(200)
        {
            FullMode     = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false
        });

        int discovered = 0;
        int processed  = 0;

        // Productor: enumera archivos y los escribe en el canal
        var producer = Task.Run(async () =>
        {
            try
            {
                foreach (var filePath in fileStream)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    Interlocked.Increment(ref discovered);
                    await channel.Writer.WriteAsync(filePath, cancellationToken);
                }
            }
            finally { channel.Writer.Complete(); }
        }, cancellationToken);

        // SemaphoreSlim(8): SHA256 es CPU-bound, más paralelismo que el I/O de thumbnails
        using var hashSem = new SemaphoreSlim(8, 8);
        var pendingBatch  = new ConcurrentBag<Photo>();
        var batchLock     = new SemaphoreSlim(1, 1);
        var pendingTasks  = new List<Task>();

        // Consumidor: lee del canal y procesa archivos en paralelo
        var consumer = Task.Run(async () =>
        {
            await foreach (var filePath in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var localPath = filePath;
                var task = Task.Run(async () =>
                {
                    await hashSem.WaitAsync(cancellationToken);
                    try
                    {
                        var hash = ComputeHash(localPath);
                        if (await _photoRepository.PhotoExistsAsync(hash))
                        {
                            Interlocked.Increment(ref processed);
                            return;
                        }

                        var photo = await CreatePhotoFromFileAsync(localPath, hash);

                        // Acumular en batch thread-safe; vaciar si llega a 100
                        List<Photo>? batchToSave = null;
                        await batchLock.WaitAsync(cancellationToken);
                        try
                        {
                            pendingBatch.Add(photo);
                            if (pendingBatch.Count >= 100)
                                batchToSave = DrainBatch(pendingBatch);
                        }
                        finally { batchLock.Release(); }

                        if (batchToSave != null)
                            await SaveBatchAsync(batchToSave, cancellationToken);

                        Interlocked.Increment(ref processed);

                        // Emitir progreso desde el primer archivo (no al final)
                        progress.TotalFound  = Volatile.Read(ref discovered);
                        progress.Processed   = Volatile.Read(ref processed);
                        progress.CurrentFile = Path.GetFileName(localPath);
                        OnProgressChanged(progress);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("[Indexer] Skipped '{File}': {Msg}", localPath, ex.Message);
                        Interlocked.Increment(ref processed);
                    }
                    finally { hashSem.Release(); }
                }, cancellationToken);

                pendingTasks.Add(task);

                // Evitar acumulación ilimitada de tareas
                if (pendingTasks.Count >= 32)
                {
                    await Task.WhenAny(pendingTasks);
                    pendingTasks.RemoveAll(t => t.IsCompleted);
                }
            }

            await Task.WhenAll(pendingTasks);
        }, cancellationToken);

        await Task.WhenAll(producer, consumer);

        // Vaciar el batch residual
        var remaining = DrainBatch(pendingBatch);
        if (remaining.Count > 0)
            await SaveBatchAsync(remaining, cancellationToken);

        _logger.LogInformation("[Indexer] Completado: {Processed}/{Total} fotos",
            processed, discovered);
    }

    private static List<Photo> DrainBatch(ConcurrentBag<Photo> bag)
    {
        var result = new List<Photo>();
        while (bag.TryTake(out var item))
            result.Add(item);
        return result;
    }

    private async Task SaveBatchAsync(List<Photo> batch, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[Indexer] Guardando batch de {Count} fotos", batch.Count);

        // Fase 1: INSERT secuencial — necesitamos los IDs asignados antes de continuar
        foreach (var photo in batch)
        {
            var photoId = await _photoRepository.InsertPhotoAsync(photo);
            photo.Id    = photoId;
        }

        // Fase 2: Thumbnails en paralelo (I/O bound, 4 concurrentes máx.)
        using var thumbSem  = new SemaphoreSlim(4, 4);
        var thumbTasks = batch.Select(async photo =>
        {
            await thumbSem.WaitAsync(cancellationToken);
            try
            {
                var tp = await _thumbnailService.GenerateThumbnailAsync(photo.Path, cancellationToken);
                photo.ThumbnailPath = tp;
                await _photoRepository.UpdateThumbnailPathAsync(photo.Id, tp);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[Indexer] Thumbnail foto {Id}: {Msg}", photo.Id, ex.Message);
            }
            finally { thumbSem.Release(); }
        });
        await Task.WhenAll(thumbTasks);

        // Fase 3: Agentes IA en paralelo (modelos ONNX son thread-safe, 2 concurrentes por RAM)
        using var agentSem  = new SemaphoreSlim(2, 2);
        var agentTasks = batch.Select(async photo =>
        {
            if (cancellationToken.IsCancellationRequested) return;
            await agentSem.WaitAsync(cancellationToken);
            try
            {
                await _agentOrchestrator.ProcessPhotoAsync(photo, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("[Indexer] Error agentes foto {Id}: {Msg}", photo.Id, ex.Message);
            }
            finally { agentSem.Release(); }
        });
        await Task.WhenAll(agentTasks);
    }

    private async Task<Photo> CreatePhotoFromFileAsync(string filePath, string hash)
    {
        var fileInfo   = new FileInfo(filePath);
        var dimensions = await GetImageDimensionsAsync(filePath);
        var metadata   = await _metadataService.ExtractMetadataAsync(filePath);

        return new Photo
        {
            Path          = filePath,
            Hash          = hash,
            FileSize      = fileInfo.Length,
            DateTaken     = metadata.DateTaken ?? fileInfo.LastWriteTime,
            Width         = dimensions.Width,
            Height        = dimensions.Height,
            Latitude      = metadata.Latitude,
            Longitude     = metadata.Longitude,
            CameraModel   = metadata.CameraModel,
            LensModel     = metadata.LensModel,
            Aperture      = metadata.Aperture,
            ShutterSpeed  = metadata.ShutterSpeed,
            Iso           = metadata.Iso,
            FocalLength   = metadata.FocalLength,
            Orientation   = metadata.Orientation,
            Status        = "Indexed",
            CreatedAt     = DateTime.UtcNow,
            LastIndexedAt = DateTime.UtcNow
        };
    }

    private static string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static async Task<(int Width, int Height)> GetImageDimensionsAsync(string filePath)
    {
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load(filePath);
            return (image.Width, image.Height);
        }
        catch
        {
            return (0, 0);
        }
    }

    private void OnProgressChanged(IndexingProgress progress)
        => ProgressChanged?.Invoke(this, progress);
}
