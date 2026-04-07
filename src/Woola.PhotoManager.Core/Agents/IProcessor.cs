namespace Woola.PhotoManager.Core.Agents;

/// <summary>
/// B1: Procesador genérico para cualquier tipo de media.
/// IAgent es IProcessor&lt;Photo&gt;.
/// Permite reutilizar el pipeline en Woola.Videos, Woola.Documents, etc.
/// </summary>
public interface IProcessor<T>
{
    string Name        { get; }
    string Description { get; }
    int    Priority    { get; }
    bool   IsEnabled   { get; set; }

    bool CanProcess(T item);
    Task<ProcessorResult> ProcessAsync(T item, CancellationToken ct = default);
}

/// <summary>Resultado de un procesador genérico.</summary>
public sealed class ProcessorResult
{
    public bool    Success      { get; init; } = true;
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration    { get; init; }
    public IReadOnlyList<ProcessorTag> Tags { get; init; } = Array.Empty<ProcessorTag>();

    public static ProcessorResult Empty => new();

    public static ProcessorResult Fail(string msg, TimeSpan t) =>
        new() { Success = false, ErrorMessage = msg, Duration = t };
}

/// <summary>Tag producido por un procesador genérico.</summary>
public sealed class ProcessorTag
{
    public string Name       { get; init; } = string.Empty;
    public string Category   { get; init; } = string.Empty;
    public float  Confidence { get; init; } = 1.0f;
}
