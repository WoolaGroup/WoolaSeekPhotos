namespace Woola.PhotoManager.Backend.Application.Common.Interfaces;

public class AgentTagResult
{
    public string AgentName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public long ProcessingTimeMs { get; set; }
    public int TagsGenerated { get; set; }
}

public interface IAgentOrchestrator
{
    Task<List<AgentTagResult>> ProcessPhotoAsync(int photoId, CancellationToken ct = default);
    bool IsAgentEnabled(string agentName);
    void SetAgentEnabled(string agentName, bool enabled);
    Dictionary<string, bool> GetAgentStates();
}
