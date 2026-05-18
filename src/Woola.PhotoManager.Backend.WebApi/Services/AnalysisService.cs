using Woola.PhotoManager.Backend.Application.Common.Interfaces;
using Woola.PhotoManager.Infrastructure.Repositories;
using CoreAgent = Woola.PhotoManager.Core.Agents;

namespace Woola.PhotoManager.Backend.WebApi.Services;

public class AnalysisService : IAnalysisService
{
    private readonly CoreAgent.IAgentOrchestrator _orchestrator;
    private readonly PhotoRepository _photoRepo;
    private readonly ILogger<AnalysisService> _logger;

    public AnalysisService(
        CoreAgent.IAgentOrchestrator orchestrator,
        PhotoRepository photoRepo,
        ILogger<AnalysisService> logger)
    {
        _orchestrator = orchestrator;
        _photoRepo = photoRepo;
        _logger = logger;
    }

    public async Task<AnalysisResult> AnalyzePendingPhotosAsync(
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken ct = default)
    {
        var photos = await _photoRepo.GetPhotosAsync(limit: 500);
        var pending = photos.Where(p => p.Status != "Analyzed").ToList();

        _logger.LogInformation("Analyzing {Count} pending photos", pending.Count);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var totalTags = 0;
        var processed = 0;

        foreach (var photo in pending)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var result = await _orchestrator.ProcessPhotoAsync(photo, ct);
                totalTags += result.Tags?.Count ?? 0;
                await _photoRepo.UpdatePhotoStatusAsync(photo.Id, "Analyzed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing photo {Id}", photo.Id);
            }

            processed++;
            progress?.Report(new AnalysisProgress
            {
                TotalPending = pending.Count,
                Processed = processed,
                CurrentPhotoId = photo.Id,
                CurrentAction = $"Analyzing {photo.FileName}"
            });
        }

        sw.Stop();
        return new AnalysisResult
        {
            PhotosAnalyzed = processed,
            TagsGenerated = totalTags,
            ElapsedMs = sw.ElapsedMilliseconds
        };
    }

    public async Task<AnalysisResult> AnalyzePhotoAsync(int photoId, CancellationToken ct = default)
    {
        var photo = await _photoRepo.GetPhotoByIdAsync(photoId);
        if (photo == null) return new AnalysisResult();

        var result = await _orchestrator.ProcessPhotoAsync(photo, ct);
        await _photoRepo.UpdatePhotoStatusAsync(photoId, "Analyzed");

        return new AnalysisResult
        {
            PhotosAnalyzed = 1,
            TagsGenerated = result.Tags?.Count ?? 0
        };
    }
}
