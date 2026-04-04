using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
    // Formato: (hMin, hMax, sMin, vMin) - neutrales usan sMax y vMax en lugar de hMin/hMax
    private static readonly (string Name, float HMin, float HMax, float SMin, float VMin, float VMax)[] _hsvColors =
    {
        ("rojo",    350, 360,  50, 20, 100),
        ("rojo",      0,  10,  50, 20, 100),   // rojo tiene dos rangos de hue
        ("naranja",  10,  30,  50, 20, 100),
        ("amarillo", 30,  60,  50, 40, 100),
        ("verde",    60, 150,  40, 20, 100),
        ("cian",    150, 195,  40, 30, 100),
        ("azul",    195, 260,  40, 20, 100),
        ("morado",  260, 310,  30, 20, 100),
        ("rosa",    310, 350,  30, 60, 100),
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
                    Name = detection.ClassName,
                    Category = "Object",
                    Confidence = detection.Confidence,
                    Source = Name
                });
            }

            // Detectar colores dominantes con confianza basada en proporción de píxeles
            var dominantColors = DetectDominantColors(photo.Path);
            foreach (var (colorName, confidence) in dominantColors)
            {
                result.Tags.Add(new AgentTag
                {
                    Name = $"color_{colorName}",
                    Category = "Color",
                    Confidence = confidence,
                    Source = Name
                });
            }

            // Tag especial si hay personas
            if (detections.Any(d => d.ClassName == "persona"))
            {
                result.Tags.Add(new AgentTag
                {
                    Name = "contiene_personas",
                    Category = "Scene",
                    Confidence = 0.9,
                    Source = Name
                });
            }

            // Tag especial si hay múltiples objetos
            if (detections.Count >= 5)
            {
                result.Tags.Add(new AgentTag
                {
                    Name = "muchos_objetos",
                    Category = "Scene",
                    Confidence = 0.8,
                    Source = Name
                });
            }

            result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Detecta colores dominantes usando espacio HSV para mayor precisión.
    /// Retorna colores con su porcentaje de presencia como confianza.
    /// </summary>
    private List<(string Name, float Confidence)> DetectDominantColors(string imagePath)
    {
        var results = new List<(string Name, float Confidence)>();

        try
        {
            using var image = Image.Load<Rgb24>(imagePath);

            // Paso de muestreo adaptativo: ~20px en imágenes grandes, 10px en pequeñas
            var step = Math.Max(10, Math.Min(image.Width, image.Height) / 100);

            var colorCounts = new Dictionary<string, int>();
            var achromaticCount = 0; // negro, blanco, gris
            var totalSamples = 0;

            for (int y = 0; y < image.Height; y += step)
            {
                for (int x = 0; x < image.Width; x += step)
                {
                    var p = image[x, y];
                    RgbToHsv(p.R, p.G, p.B, out var h, out var s, out var v);
                    totalSamples++;

                    // Clasificar acromáticos primero (sin saturación relevante)
                    if (v < 15f) { achromaticCount++; continue; }            // negro
                    if (v > 85f && s < 15f) { achromaticCount++; continue; } // blanco
                    if (s < 20f) { achromaticCount++; continue; }            // gris

                    // Clasificar cromáticos por rango HSV
                    var colorName = GetHsvColorName(h, s, v);
                    if (colorName != null)
                    {
                        colorCounts.TryGetValue(colorName, out var cnt);
                        colorCounts[colorName] = cnt + 1;
                    }
                }
            }

            if (totalSamples == 0) return results;

            // Solo reportar colores con al menos 8% de presencia
            const float MinThreshold = 0.08f;
            foreach (var kv in colorCounts.OrderByDescending(k => k.Value))
            {
                var ratio = kv.Value / (float)totalSamples;
                if (ratio >= MinThreshold)
                    results.Add((kv.Key, Math.Min(ratio * 1.5f, 1.0f))); // escalar confianza
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VisionAgent] Error detectando colores: {ex.Message}");
        }

        return results.Take(3).ToList(); // máximo 3 colores dominantes
    }

    /// <summary>
    /// Convierte RGB (0-255) a HSV: H en grados (0-360), S y V en porcentaje (0-100).
    /// </summary>
    private static void RgbToHsv(byte r, byte g, byte b, out float h, out float s, out float v)
    {
        var rf = r / 255f;
        var gf = g / 255f;
        var bf = b / 255f;

        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
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

    /// <summary>
    /// Clasifica un píxel HSV en un nombre de color. Retorna null si no encaja en ningún rango.
    /// </summary>
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
