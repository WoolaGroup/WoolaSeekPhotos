using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

public class PhotoIndexer : IPhotoIndexer
{
    private readonly PhotoRepository _photoRepository;
    private readonly TagRepository _tagRepository;
    private readonly IThumbnailService _thumbnailService;
    private readonly IMetadataService _metadataService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;

    private readonly IAutoTaggingService _autoTaggingService;

    public PhotoIndexer(
        PhotoRepository photoRepository,
        TagRepository tagRepository,
        IThumbnailService thumbnailService,
        IMetadataService metadataService,
        IAutoTaggingService autoTaggingService)  // ← Nuevo parámetro
    {
        _photoRepository = photoRepository;
        _tagRepository = tagRepository;
        _thumbnailService = thumbnailService;
        _metadataService = metadataService;
        _autoTaggingService = autoTaggingService;  // ← Inicializar
    }

    private static readonly HashSet<string> SupportedExtensions = new(
        new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" },
        StringComparer.OrdinalIgnoreCase);

    public event EventHandler<IndexingProgress>? ProgressChanged;
    public bool IsRunning => _isRunning;

    public PhotoIndexer(
        PhotoRepository photoRepository,
        TagRepository tagRepository,
        IThumbnailService thumbnailService,
        IMetadataService metadataService)
    {
        _photoRepository = photoRepository;
        _tagRepository = tagRepository;
        _thumbnailService = thumbnailService;
        _metadataService = metadataService;
    }

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

    private async Task IndexDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        var allFiles = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .ToList();

        var progress = new IndexingProgress { TotalFound = allFiles.Count };
        var processed = 0;
        var batch = new List<Photo>();

        OnProgressChanged(progress);

        foreach (var filePath in allFiles)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var hash = ComputeHash(filePath);
                if (await _photoRepository.PhotoExistsAsync(hash)) continue;

                var photo = await CreatePhotoFromFileAsync(filePath, hash);
                batch.Add(photo);

                if (batch.Count >= 100)
                {
                    await SaveBatchAsync(batch, cancellationToken);
                    batch.Clear();
                }

                processed++;
                progress.Processed = processed;
                progress.CurrentFile = Path.GetFileName(filePath);
                OnProgressChanged(progress);
            }
            catch
            {
                processed++;
            }
        }

        if (batch.Count > 0)
            await SaveBatchAsync(batch, cancellationToken);
    }

    private async Task SaveBatchAsync(List<Photo> batch, CancellationToken cancellationToken)
    {
        foreach (var photo in batch)
        {
            var photoId = await _photoRepository.InsertPhotoAsync(photo);
            photo.Id = photoId;

            // ✅ Generar y aplicar tags automáticos
            var tags = await _autoTaggingService.GenerateTagsForPhotoAsync(photo);
            await _autoTaggingService.ApplyTagsToPhotoAsync(photoId, tags);
        }

        foreach (var photo in batch)
        {
            try
            {
                var thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(photo.Path, cancellationToken);
                photo.ThumbnailPath = thumbnailPath;
                await _photoRepository.UpdateThumbnailPathAsync(photo.Id, thumbnailPath);
            }
            catch { }
        }
    }

    private async Task<Photo> CreatePhotoFromFileAsync(string filePath, string hash)
    {
        var fileInfo = new FileInfo(filePath);
        var dimensions = await GetImageDimensionsAsync(filePath);

        // Extraer metadata EXIF
        var metadata = await _metadataService.ExtractMetadataAsync(filePath);

        return new Photo
        {
            Path = filePath,
            Hash = hash,
            FileSize = fileInfo.Length,
            DateTaken = metadata.DateTaken ?? fileInfo.LastWriteTime,
            Width = dimensions.Width,
            Height = dimensions.Height,
            Latitude = metadata.Latitude,
            Longitude = metadata.Longitude,
            CameraModel = metadata.CameraModel,
            LensModel = metadata.LensModel,
            Aperture = metadata.Aperture,
            ShutterSpeed = metadata.ShutterSpeed,
            Iso = metadata.Iso,
            FocalLength = metadata.FocalLength,
            Orientation = metadata.Orientation,
            Status = "Indexed",
            CreatedAt = DateTime.UtcNow,
            LastIndexedAt = DateTime.UtcNow
        };
    }

    private string ComputeHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private async Task<(int Width, int Height)> GetImageDimensionsAsync(string filePath)
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
    {
        ProgressChanged?.Invoke(this, progress);
    }



}