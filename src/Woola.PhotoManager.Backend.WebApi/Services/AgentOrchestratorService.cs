using Woola.PhotoManager.Backend.Application.Common.Interfaces;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;
using CoreAgent = Woola.PhotoManager.Core.Agents;

namespace Woola.PhotoManager.Backend.WebApi.Services;

public class AgentOrchestratorService : IAgentOrchestrator
{
    private readonly CoreAgent.IAgentOrchestrator _orchestrator;
    private readonly PhotoRepository _photoRepo;
    private readonly TagRepository _tagRepo;
    private readonly ILogger<AgentOrchestratorService> _logger;

    public AgentOrchestratorService(
        CoreAgent.IAgentOrchestrator orchestrator,
        PhotoRepository photoRepo,
        TagRepository tagRepo,
        ILogger<AgentOrchestratorService> logger)
    {
        _orchestrator = orchestrator;
        _photoRepo = photoRepo;
        _tagRepo = tagRepo;
        _logger = logger;
    }

    public bool IsAgentEnabled(string agentName)
    {
        return agentName switch
        {
            "ClaudeVisionAgent" => false,
            _ => true
        };
    }

    public void SetAgentEnabled(string agentName, bool enabled)
    {
        _logger.LogInformation("Agent {Agent} set to {Enabled}", agentName, enabled);
    }

    public Dictionary<string, bool> GetAgentStates() => new()
    {
        ["MetadataAgent"] = true,
        ["AutoTaggingAgent"] = true,
        ["VisionAgent"] = true,
        ["FaceAgent"] = true,
        ["OcrAgent"] = true,
        ["SceneAgent"] = true,
        ["QualityAgent"] = true,
        ["GeoLocationAgent"] = true,
        ["ClaudeVisionAgent"] = false,
    };

    public async Task<List<AgentTagResult>> ProcessPhotoAsync(int photoId, CancellationToken ct = default)
    {
        var photo = await _photoRepo.GetPhotoByIdAsync(photoId);
        if (photo == null) return new();

        var result = await _orchestrator.ProcessPhotoAsync(photo, ct);
        return new List<AgentTagResult>
        {
            new()
            {
                AgentName = result.AgentName ?? "Unknown",
                Success = result.Success,
                ErrorMessage = result.ErrorMessage,
                ProcessingTimeMs = (long)result.ProcessingTimeMs,
                TagsGenerated = result.Tags?.Count ?? 0
            }
        };
    }
}
