using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

/// <summary>
/// D4: Evalúa calidad de imagen (nitidez, exposición, resolución) y genera tags semánticos.
/// Prioridad 7 — corre al final del pipeline, no depende de otros agentes.
/// Tags generados:
///   foto_borrosa | foto_nitida
///   subexpuesta | sobreexpuesta | exposición_normal
///   alta_resolución | baja_resolución
///   calidad_alta | calidad_media | calidad_baja
/// </summary>
public class QualityAgent : IAgent
{
    private readonly IQualityAssessmentService _qualityService;

    public string Name => "QualityAgent";
    public string Description => "Evalúa calidad: nitidez, exposición y resolución";
    public int Priority => 7;
    public bool IsEnabled { get; set; } = true;

    public QualityAgent(IQualityAssessmentService qualityService)
    {
        _qualityService = qualityService;
    }

    public bool CanProcess(Photo photo)
    {
        var ext = Path.GetExtension(photo.Path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".bmp" && File.Exists(photo.Path);
    }

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            var q = await _qualityService.AssessAsync(photo.Path);

            // ── Nitidez ────────────────────────────────────────────────────────
            if (q.BlurScore < 0.15f)
                result.Tags.Add(Tag("foto_borrosa", 1.0 - q.BlurScore));
            else if (q.BlurScore > 0.50f)
                result.Tags.Add(Tag("foto_nitida", q.BlurScore));

            // ── Exposición ─────────────────────────────────────────────────────
            if (q.Brightness < 0.15f)
                result.Tags.Add(Tag("subexpuesta", Math.Min(1.0 - q.Brightness * 5.0, 0.95)));
            else if (q.Brightness > 0.85f)
                result.Tags.Add(Tag("sobreexpuesta", Math.Min((double)q.Brightness, 0.95)));
            else
                result.Tags.Add(Tag("exposición_normal", 0.8));

            // ── Resolución ─────────────────────────────────────────────────────
            long mp = (long)q.Width * q.Height;
            if (mp >= 8_000_000)
                result.Tags.Add(Tag("alta_resolución", 0.9));
            else if (mp > 0 && mp < 1_000_000)
                result.Tags.Add(Tag("baja_resolución", 0.9));

            // ── Puntuación global ──────────────────────────────────────────────
            // 50% nitidez + 30% exposición centrada + 20% resolución
            double expScore = 1.0 - Math.Abs(q.Brightness - 0.5) * 2.0;
            double resScore = mp >= 4_000_000 ? 1.0 : (mp > 0 ? mp / 4_000_000.0 : 0.5);
            double qualityScore = q.BlurScore * 0.5 + expScore * 0.3 + resScore * 0.2;

            if (qualityScore >= 0.70)
                result.Tags.Add(Tag("calidad_alta", qualityScore));
            else if (qualityScore >= 0.40)
                result.Tags.Add(Tag("calidad_media", qualityScore));
            else
                result.Tags.Add(Tag("calidad_baja", 1.0 - qualityScore));

            result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static AgentTag Tag(string name, double confidence) => new AgentTag
    {
        Name = name,
        Category = "Quality",
        Confidence = confidence,
        Source = "QualityAgent"
    };
}
