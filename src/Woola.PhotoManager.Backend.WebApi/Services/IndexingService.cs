using Microsoft.AspNetCore.SignalR;
using Woola.PhotoManager.Backend.Application.Common.Interfaces;
using Woola.PhotoManager.Backend.WebApi.Hubs;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Infrastructure.Repositories;
using Woola.PhotoManager.Common.Services;
using CoreAgent = Woola.PhotoManager.Core.Agents;

namespace Woola.PhotoManager.Backend.WebApi.Services;

public class IndexingService : IIndexingService, IAsyncDisposable
{
    private readonly PhotoIndexer _photoIndexer;
    private readonly IHubContext<IndexingHub> _hubContext;
    private readonly ILogger<IndexingService> _logger;
    private CancellationTokenSource? _cts;

    public bool IsIndexing => _photoIndexer.IsRunning;

    public IndexingService(
        PhotoRepository photoRepository,
        TagRepository tagRepository,
        IThumbnailService thumbnailService,
        IMetadataService metadataService,
        CoreAgent.IAgentOrchestrator agentOrchestrator,
        IHubContext<IndexingHub> hubContext,
        ILogger<IndexingService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;

        _photoIndexer = new PhotoIndexer(
            photoRepository, tagRepository, thumbnailService,
            metadataService, agentOrchestrator,
            logger as ILogger<PhotoIndexer> ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PhotoIndexer>());
    }

    public Task CancelIndexingAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    public async Task<IndexJobResult> StartIndexingAsync(
        string folderPath,
        IProgress<IndexProgress>? progress = null,
        CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var totalFound = 0;
        var processed = 0;
        var errors = 0;

        _photoIndexer.ProgressChanged += OnProgress;

        try
        {
            await _photoIndexer.StartIndexingAsync(folderPath, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Indexing cancelled by user");
        }
        catch (Exception ex)
        {
            errors++;
            _logger.LogError(ex, "Indexing error");
        }
        finally
        {
            _photoIndexer.ProgressChanged -= OnProgress;
            sw.Stop();
        }

        return new IndexJobResult
        {
            NewPhotos = processed,
            TotalPhotos = totalFound,
            DuplicatesSkipped = totalFound - processed,
            Errors = errors,
            ElapsedMs = sw.ElapsedMilliseconds,
            Status = _cts.IsCancellationRequested ? "Cancelled" : "Completed"
        };

        void OnProgress(object? sender, Core.Services.IndexingProgress e)
        {
            totalFound = e.TotalFound;
            processed = e.Processed;

            progress?.Report(new IndexProgress
            {
                TotalFound = e.TotalFound,
                Processed = e.Processed,
                CurrentFile = e.CurrentFile ?? string.Empty,
                Status = e.TotalFound > 0 && e.Processed < e.TotalFound
                    ? $"Indexing {e.Processed}/{e.TotalFound}"
                    : "Idle",
                ElapsedMs = sw.ElapsedMilliseconds
            });

            _ = _hubContext.Clients.Group("indexing").SendAsync("ProgressReceived", new
            {
                totalFound = e.TotalFound,
                processed = e.Processed,
                percentage = e.TotalFound > 0 ? Math.Round((double)e.Processed / e.TotalFound * 100, 1) : 0,
                currentFile = e.CurrentFile ?? string.Empty
            }, _cts.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Dispose();
        await Task.CompletedTask;
    }
}
