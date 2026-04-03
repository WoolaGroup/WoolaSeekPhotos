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

    public AgentOrchestrator(TagRepository tagRepository)
    {
        _tagRepository = tagRepository;
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
        {
            agent.IsEnabled = enable;
        }
    }

    public List<IAgent> GetAgents() => _agents.ToList();

    public async Task<AgentResult> ProcessPhotoAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var combinedResult = new AgentResult { AgentName = "Orchestrator", Success = true };

        foreach (var agent in _agents.Where(a => a.IsEnabled))
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (!agent.CanProcess(photo)) continue;

            try
            {
                var result = await agent.ExecuteAsync(photo, cancellationToken);

                if (result.Success && result.Tags.Any())
                {
                    combinedResult.Tags.AddRange(result.Tags);
                    await SaveTagsToDatabase(photo.Id, result.Tags, agent.Name);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en agente {agent.Name}: {ex.Message}");
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