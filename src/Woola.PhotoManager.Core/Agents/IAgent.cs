using System.Diagnostics;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents;

/// <summary>
/// B1: IAgent extiende IProcessor&lt;Photo&gt;.
/// Todos los agentes existentes siguen funcionando sin cambios.
/// ProcessAsync bridgea automáticamente hacia ExecuteAsync.
/// </summary>
public interface IAgent : IProcessor<Photo>
{
    /// <summary>Método legacy de ejecución. Usado internamente por el orquestador.</summary>
    Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Implementación por defecto de IProcessor&lt;Photo&gt;.ProcessAsync.
    /// Convierte AgentResult → ProcessorResult para compatibilidad con ProcessorPipeline.
    /// </summary>
    Task<ProcessorResult> IProcessor<Photo>.ProcessAsync(Photo item, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        return ExecuteAsync(item, ct).ContinueWith(t =>
        {
            sw.Stop();
            if (t.IsFaulted)
                return ProcessorResult.Fail(t.Exception!.InnerException?.Message ?? "Error", sw.Elapsed);

            var r = t.Result;
            return new ProcessorResult
            {
                Success      = r.Success,
                ErrorMessage = r.ErrorMessage,
                Duration     = sw.Elapsed,
                Tags         = r.Tags.Select(tag => new ProcessorTag
                {
                    Name       = tag.Name,
                    Category   = tag.Category,
                    Confidence = (float)tag.Confidence
                }).ToArray()
            };
        }, TaskContinuationOptions.ExecuteSynchronously);
    }
}

public class AgentResult
{
    public string AgentName       { get; set; } = string.Empty;
    public List<AgentTag> Tags    { get; set; } = new();
    public bool   Success         { get; set; }
    public string? ErrorMessage   { get; set; }
    public double  ProcessingTimeMs { get; set; }
}

public class AgentTag
{
    public string Name       { get; set; } = string.Empty;
    public string Category   { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Source     { get; set; } = string.Empty;
}
