using Microsoft.Extensions.Logging;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Agents;

public interface IAgentOrchestrator
{
    void RegisterAgent(IAgent agent);
    void EnableAgent(string agentName, bool enable);
    Task<AgentResult> ProcessPhotoAsync(Photo photo, CancellationToken cancellationToken = default);
    List<IAgent> GetAgents();
}

/// <summary>
/// B3: AgentOrchestrator usa ProcessorPipeline&lt;Photo&gt; internamente para
/// el registro y listado de agentes.
/// ProcessPhotoAsync mantiene el bucle per-agente para atribuir el Source a cada tag en DB.
/// </summary>
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly ProcessorPipeline<Photo>       _pipeline;
    private readonly TagRepository                  _tagRepository;
    private readonly ILogger<AgentOrchestrator>     _logger;

    private const int AgentTimeoutSeconds = 30;

    public AgentOrchestrator(TagRepository tagRepository, ILogger<AgentOrchestrator> logger)
    {
        _tagRepository = tagRepository;
        _logger        = logger;
        _pipeline      = new ProcessorPipeline<Photo>(logger);
    }

    /// <summary>Registra un agente en el pipeline (se ordenan por Priority en ejecución).</summary>
    public void RegisterAgent(IAgent agent) => _pipeline.Register(agent);

    /// <summary>Habilita o deshabilita un agente por nombre.</summary>
    public void EnableAgent(string agentName, bool enable)
    {
        var proc = _pipeline.All.FirstOrDefault(p => p.Name == agentName);
        if (proc != null) proc.IsEnabled = enable;
    }

    /// <summary>Devuelve todos los agentes registrados (los que implementan IAgent).</summary>
    public List<IAgent> GetAgents() => _pipeline.All.OfType<IAgent>().ToList();

    /// <summary>
    /// Ejecuta todos los agentes habilitados sobre una foto y persiste los tags en DB.
    /// Mantiene atribución por Source (agent.Name) para trazabilidad en PhotoTags.
    /// </summary>
    public async Task<AgentResult> ProcessPhotoAsync(
        Photo photo, CancellationToken cancellationToken = default)
    {
        var combinedResult = new AgentResult { AgentName = "Orchestrator", Success = true };

        // Iterar en orden de prioridad, igual que ProcessorPipeline.RunAsync
        foreach (var agent in GetAgents()
                     .Where(a => a.IsEnabled)
                     .OrderBy(a => a.Priority))
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (!agent.CanProcess(photo)) continue;

            using var agentTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            agentTimeout.CancelAfter(TimeSpan.FromSeconds(AgentTimeoutSeconds));

            try
            {
                var result = await agent.ExecuteAsync(photo, agentTimeout.Token);

                if (result.Success && result.Tags.Count > 0)
                {
                    combinedResult.Tags.AddRange(result.Tags);
                    await SaveTagsToDatabaseAsync(photo.Id, result.Tags, agent.Name);
                    _logger.LogDebug("{Agent} procesó foto {PhotoId}: {TagCount} tags en {Ms}ms",
                        agent.Name, photo.Id, result.Tags.Count, (int)result.ProcessingTimeMs);
                }
                else if (!result.Success)
                {
                    _logger.LogWarning("{Agent} falló en foto {PhotoId}: {Error}",
                        agent.Name, photo.Id, result.ErrorMessage);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("{Agent} superó el timeout de {Seconds}s en foto {PhotoId}",
                    agent.Name, AgentTimeoutSeconds, photo.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Agent} lanzó excepción en foto {PhotoId}",
                    agent.Name, photo.Id);
            }
        }

        return combinedResult;
    }

    private async Task SaveTagsToDatabaseAsync(int photoId, List<AgentTag> tags, string source)
    {
        foreach (var tag in tags)
        {
            var tagId = await _tagRepository.GetOrCreateTagAsync(tag.Name, tag.Category, true);
            await _tagRepository.AddTagToPhotoAsync(photoId, tagId, tag.Confidence, source);
        }
    }
}
