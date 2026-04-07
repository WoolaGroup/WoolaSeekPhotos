using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

/// <summary>
/// A4: AutoTaggingAgent delega en IAutoTaggingService.
/// Elimina la duplicación de lógica que existía en las 115 líneas anteriores.
/// </summary>
public class AutoTaggingAgent : IAgent
{
    private readonly IAutoTaggingService _svc;

    public string Name        => "AutoTaggingAgent";
    public string Description => "Genera tags automáticos por fecha, ubicación y cámara";
    public int    Priority    => 2;
    public bool   IsEnabled   { get; set; } = true;

    public AutoTaggingAgent(IAutoTaggingService svc) => _svc = svc;

    public bool CanProcess(Photo photo) => true;

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            var tagNames = await _svc.GenerateTagsForPhotoAsync(photo);

            result.Tags = tagNames.Select(n => new AgentTag
            {
                Name       = n,
                Category   = "AutoTag",
                Confidence = 1.0,
                Source     = Name
            }).ToList();
        }
        catch (Exception ex)
        {
            result.Success      = false;
            result.ErrorMessage = ex.Message;
        }

        result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        return result;
    }
}
