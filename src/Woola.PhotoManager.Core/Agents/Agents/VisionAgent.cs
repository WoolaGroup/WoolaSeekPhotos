using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Woola.PhotoManager.Common.Models;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

public class VisionAgent : IAgent
{
    private readonly IObjectDetectionService _objectDetectionService;

    public string Name => "VisionAgent";
    public string Description => "Detecta objetos y colores en imágenes usando YOLO";
    public int Priority => 3;
    public bool IsEnabled { get; set; } = true;

    // Rangos HSV para detección de colores. H: 0-360°, S: 0-100%, V: 0-100%
    private static readonly (string Name, float HMin, float HMax, float SMin, float VMin, float VMax)[] _hsvColors =
    {
        ("rojo",    350, 360,  50, 20, 100),
        ("rojo",      0,  10,  50, 20, 100),
        ("naranja",  10,  30,  50, 20, 100),
        ("amarillo", 30,  60,  50, 40, 100),
        ("verde",    60, 150,  40, 20, 100),
        ("cian",    150, 195,  40, 30, 100),
        ("azul",    195, 260,  40, 20, 100),
        ("morado",  260, 310,  30, 20, 100),
        ("rosa",    310, 350,  30, 60, 100),
    };

    /// <summary>
    /// A2: Objetos que NO generan tag compuesto (evitar ruido por color de piel, etc.).
    /// Incluye nombres en inglés (YOLO) y español (por compatibilidad futura).
    /// </summary>
    private static readonly HashSet<string> _noCompoundObjects = new(StringComparer.OrdinalIgnoreCase)
    {
        "person", "face", "hand",
        "persona", "cara", "mano", "hombre", "mujer", "niño", "niña"
    };

    public VisionAgent(IObjectDetectionService objectDetectionService)
    {
        _objectDetectionService = objectDetectionService;
    }

    public bool CanProcess(Photo photo)
    {
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
        var ext = Path.GetExtension(photo.Path).ToLower();
        return extensions.Contains(ext) && File.Exists(photo.Path);
    }

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            // Descargar modelo si es necesario
            await _objectDetectionService.DownloadModelIfNeededAsync();

            // Detectar objetos
            var detections = await _objectDetectionService.DetectObjectsAsync(photo.Path);

            // Agregar tags por objetos detectados
            foreach (var detection in detections)
            {
                result.Tags.Add(new AgentTag
                {
                    Name       = detection.ClassName,
                    Category   = "Object",
                    Confidence = detection.Confidence,
                    Source     = Name
                });
            }

            // Detectar colores dominantes del frame completo
            var dominantColors = DetectDominantColors(photo.Path);
            foreach (var (colorName, confidence) in dominantColors)
            {
                result.Tags.Add(new AgentTag
                {
                    Name       = $"color_{colorName}",
                    Category   = "Color",
                    Confidence = confidence,
                    Source     = Name
                });
            }

            // A2: Tags compuestos objeto+color usando bounding boxes
            var compoundCandidates = detections
                .Where(d => d.Confidence > 0.5f && !_noCompoundObjects.Contains(d.ClassName))
                .ToList();

            if (compoundCandidates.Count > 0)
            {
                // Cargar imagen UNA sola vez para todos los recortes
                using var fullImage = Image.Load<Rgb24>(photo.Path);

                foreach (var det in compoundCandidates)
                {
                    var regionColors = GetDominantColorsInRegion(fullImage, det);
                    if (regionColors.Count == 0) continue;

                    var (topColor, _) = regionColors[0]; // Solo el color más dominante

                    // No generar compound con acromáticos
                    if (topColor is "blanco" or "negro" or "gris") continue;

                    result.Tags.Add(new AgentTag
                    {
                        Name       = $"{det.ClassName}_{topColor}",
                        Category   = "Object",
                        Confidence = det.Confidence * 0.85f,
                        Source     = Name
                    });
                }
            }

            // Tag especial si hay personas
            if (detections.Any(d => d.ClassName == "person"))
            {
                result.Tags.Add(new AgentTag
                {
                    Name       = "contiene_personas",
                    Category   = "Scene",
                    Confidence = 0.9,
                    Source     = Name
                });
            }

            // Tag especial si hay múltiples objetos
            if (detections.Count >= 5)
            {
                result.Tags.Add(new AgentTag
                {
                    Name       = "muchos_objetos",
                    Category   = "Scene",
                    Confidence = 0.8,
                    Source     = Name
                });
            }

            result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            result.Success      = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    // ── Color detection ─────────────────────────────────────────────────────────

    /// <summary>
    /// Detecta colores dominantes de una imagen en disco.
    /// </summary>
    private List<(string Name, float Confidence)> DetectDominantColors(string imagePath)
    {
        try
        {
            using var image = Image.Load<Rgb24>(imagePath);
            return DetectDominantColorsFromImage(image);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VisionAgent] Error cargando imagen para colores: {ex.Message}");
            return new();
        }
    }

    /// <summary>
    /// A2: Detecta el color dominante en la región del bounding box de un objeto.
    /// Coordenadas X,Y: centro normalizado [0-1]; W,H: dimensiones normalizadas [0-1].
    /// </summary>
    private List<(string Name, float Confidence)> GetDominantColorsInRegion(
        Image<Rgb24> image, DetectedObject det)
    {
        try
        {
            int imgW = image.Width;
            int imgH = image.Height;

            // Convertir coords centro-normalizadas → rect de píxeles
            int left   = (int)Math.Max(0, (det.X - det.Width  / 2f) * imgW);
            int top    = (int)Math.Max(0, (det.Y - det.Height / 2f) * imgH);
            int width  = (int)Math.Min(imgW - left, det.Width  * imgW);
            int height = (int)Math.Min(imgH - top,  det.Height * imgH);

            if (width < 4 || height < 4) return new();

            using var cropped = image.Clone(ctx =>
                ctx.Crop(new Rectangle(left, top, width, height)));

            return DetectDominantColorsFromImage(cropped);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VisionAgent] Error en recorte de bbox: {ex.Message}");
            return new();
        }
    }

    /// <summary>
    /// Detecta colores dominantes usando espacio HSV para mayor precisión.
    /// Retorna colores con su porcentaje de presencia como confianza.
    /// </summary>
    private List<(string Name, float Confidence)> DetectDominantColorsFromImage(Image<Rgb24> image)
    {
        var results = new List<(string Name, float Confidence)>();

        // Paso adaptativo: ~20px en imágenes grandes, 5px en recortes pequeños
        var step = Math.Max(5, Math.Min(image.Width, image.Height) / 50);

        var colorCounts  = new Dictionary<string, int>();
        var totalSamples = 0;

        for (int y = 0; y < image.Height; y += step)
        {
            for (int x = 0; x < image.Width; x += step)
            {
                var p = image[x, y];
                RgbToHsv(p.R, p.G, p.B, out var h, out var s, out var v);
                totalSamples++;

                if (v < 15f) continue;               // negro
                if (v > 85f && s < 15f) continue;    // blanco
                if (s < 20f) continue;               // gris

                var colorName = GetHsvColorName(h, s, v);
                if (colorName != null)
                {
                    colorCounts.TryGetValue(colorName, out var cnt);
                    colorCounts[colorName] = cnt + 1;
                }
            }
        }

        if (totalSamples == 0) return results;

        const float MinThreshold = 0.08f;
        foreach (var kv in colorCounts.OrderByDescending(k => k.Value))
        {
            var ratio = kv.Value / (float)totalSamples;
            if (ratio >= MinThreshold)
                results.Add((kv.Key, Math.Min(ratio * 1.5f, 1.0f)));
        }

        return results.Take(3).ToList();
    }

    // ── HSV helpers ─────────────────────────────────────────────────────────────

    private static void RgbToHsv(byte r, byte g, byte b, out float h, out float s, out float v)
    {
        var rf = r / 255f;
        var gf = g / 255f;
        var bf = b / 255f;

        var max   = Math.Max(rf, Math.Max(gf, bf));
        var min   = Math.Min(rf, Math.Min(gf, bf));
        var delta = max - min;

        v = max * 100f;
        s = max < 0.001f ? 0f : (delta / max) * 100f;

        if (delta < 0.001f) { h = 0f; return; }

        if (max == rf)
            h = 60f * (((gf - bf) / delta) % 6f);
        else if (max == gf)
            h = 60f * (((bf - rf) / delta) + 2f);
        else
            h = 60f * (((rf - gf) / delta) + 4f);

        if (h < 0f) h += 360f;
    }

    private static string? GetHsvColorName(float h, float s, float v)
    {
        foreach (var (name, hMin, hMax, sMin, vMin, vMax) in _hsvColors)
        {
            if (s >= sMin && v >= vMin && v <= vMax && h >= hMin && h <= hMax)
                return name;
        }
        return null;
    }
}
