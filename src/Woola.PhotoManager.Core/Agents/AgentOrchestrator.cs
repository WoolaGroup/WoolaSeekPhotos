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

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly List<IAgent> _agents = new();
    private readonly TagRepository _tagRepository;
    private readonly ILogger<AgentOrchestrator> _logger;

    // Timeout máximo por agente para que un fallo no bloquee el batch
    private const int AgentTimeoutSeconds = 30;

    public AgentOrchestrator(TagRepository tagRepository, ILogger<AgentOrchestrator> logger)
    {
        _tagRepository = tagRepository;
        _logger = logger;
    }

    public void RegisterAgent(IAgent agent)
    {
        _agents.Add(agent);
        _agents.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void EnableAgent(string agentName, bool enable)
    {
        var agent = _agents.FirstOrDefault(a => a.Name == agentName);
        if (agent != null)
            agent.IsEnabled = enable;
    }

    public List<IAgent> GetAgents() => _agents.ToList();

    public async Task<AgentResult> ProcessPhotoAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var combinedResult = new AgentResult { AgentName = "Orchestrator", Success = true };

        foreach (var agent in _agents.Where(a => a.IsEnabled))
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (!agent.CanProcess(photo)) continue;

            // Timeout individual por agente: evita que OCR/ONNX bloqueen el batch completo
            using var agentTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            agentTimeout.CancelAfter(TimeSpan.FromSeconds(AgentTimeoutSeconds));

            try
            {
                var result = await agent.ExecuteAsync(photo, agentTimeout.Token);

                if (result.Success && result.Tags.Count > 0)
                {
                    combinedResult.Tags.AddRange(result.Tags);
                    await SaveTagsToDatabase(photo.Id, result.Tags, agent.Name);
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

    private async Task SaveTagsToDatabase(int photoId, List<AgentTag> tags, string source)
    {
        foreach (var tag in tags)
        {
            var tagId = await _tagRepository.GetOrCreateTagAsync(tag.Name, tag.Category, true);
            await _tagRepository.AddTagToPhotoAsync(photoId, tagId, tag.Confidence, source);
        }
    }
}