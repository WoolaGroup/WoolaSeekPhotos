using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Woola.PhotoManager.Core.Agents;

/// <summary>
/// B2: Pipeline genérico de procesadores. Sin dependencia de base de datos.
/// Retorna los resultados — el caller decide cómo persistirlos.
/// Reutilizable para Video, Document, Music indexers.
/// </summary>
public sealed class ProcessorPipeline<T>
{
    private readonly List<IProcessor<T>> _processors = new();
    private readonly ILogger             _logger;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public ProcessorPipeline(ILogger logger) => _logger = logger;

    public void Register(IProcessor<T> processor) => _processors.Add(processor);

    public IReadOnlyList<IProcessor<T>> All => _processors.AsReadOnly();

    /// <summary>
    /// Ejecuta todos los procesadores habilitados en orden de prioridad.
    /// Cada procesador tiene un timeout de 30 segundos.
    /// Los errores se registran pero no interrumpen el pipeline.
    /// </summary>
    public async Task<PipelineRunResult> RunAsync(T item, CancellationToken ct = default)
    {
        var allTags = new List<ProcessorTag>();
        var runs    = new List<ProcessorRunInfo>();

        foreach (var proc in _processors
                     .Where(p => p.IsEnabled)
                     .OrderBy(p => p.Priority))
        {
            if (ct.IsCancellationRequested) break;
            if (!proc.CanProcess(item)) continue;

            var sw = Stopwatch.StartNew();
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(DefaultTimeout);

                var result = await proc.ProcessAsync(item, linked.Token);
                allTags.AddRange(result.Tags);
                runs.Add(new ProcessorRunInfo(proc.Name, true, sw.Elapsed));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("[Pipeline] Timeout en {Processor}", proc.Name);
                runs.Add(new ProcessorRunInfo(proc.Name, false, sw.Elapsed));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Pipeline] Error en {Processor}", proc.Name);
                runs.Add(new ProcessorRunInfo(proc.Name, false, sw.Elapsed));
            }
        }

        return new PipelineRunResult(allTags, runs);
    }
}

public sealed record PipelineRunResult(
    IReadOnlyList<ProcessorTag>     Tags,
    IReadOnlyList<ProcessorRunInfo> Runs);

public sealed record ProcessorRunInfo(
    string   Name,
    bool     Success,
    TimeSpan Duration);
