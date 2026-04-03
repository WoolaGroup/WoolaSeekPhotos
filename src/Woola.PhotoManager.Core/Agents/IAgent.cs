using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents;

public interface IAgent
{
    string Name { get; }
    string Description { get; }
    int Priority { get; }
    bool IsEnabled { get; set; }

    Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default);
    bool CanProcess(Photo photo);
}

public class AgentResult
{
    public string AgentName { get; set; } = string.Empty;
    public List<AgentTag> Tags { get; set; } = new();
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double ProcessingTimeMs { get; set; }
}

public class AgentTag
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Source { get; set; } = string.Empty;
}