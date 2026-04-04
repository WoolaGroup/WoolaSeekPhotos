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

    // Lista de colores para detectar
    private readonly Dictionary<string, (byte rMin, byte rMax, byte gMin, byte gMax, byte bMin, byte bMax)> _colorRanges = new()
    {
        { "rojo", (180, 255, 0, 100, 0, 100) },
        { "verde", (0, 100, 180, 255, 0, 100) },
        { "azul", (0, 100, 0, 100, 180, 255) },
        { "amarillo", (200, 255, 200, 255, 0, 100) },
        { "naranja", (200, 255, 100, 180, 0, 80) },
        { "morado", (150, 255, 0, 100, 150, 255) },
        { "rosa", (200, 255, 100, 180, 150, 255) },
        { "negro", (0, 50, 0, 50, 0, 50) },
        { "blanco", (200, 255, 200, 255, 200, 255) },
        { "gris", (80, 150, 80, 150, 80, 150) }
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

            // Detectar colores dominantes en la imagen
            var dominantColors = DetectDominantColors(photo.Path);
            foreach (var color in dominantColors)
            {
                result.Tags.Add(new AgentTag
                {
                    Name = $"color_{color}",
                    Category = "Color",
                    Confidence = 0.7,
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
    /// Detecta los colores dominantes en una imagen
    /// </summary>
    private List<string> DetectDominantColors(string imagePath)
    {
        var detectedColors = new List<string>();

        try
        {
            using var image = Image.Load<Rgb24>(imagePath);

            // Muestrear píxeles para detectar colores dominantes
            var colorCounts = new Dictionary<string, int>();

            // Inicializar contadores
            foreach (var colorName in _colorRanges.Keys)
            {
                colorCounts[colorName] = 0;
            }

            // Muestrear la imagen (cada 50 píxeles para rendimiento)
            for (int y = 0; y < image.Height; y += 50)
            {
                for (int x = 0; x < image.Width; x += 50)
                {
                    var pixel = image[x, y];
                    var colorName = GetColorName(pixel.R, pixel.G, pixel.B);

                    if (colorName != null && colorCounts.ContainsKey(colorName))
                    {
                        colorCounts[colorName]++;
                    }
                }
            }

            // Obtener colores con más del 10% de la muestra
            var totalSamples = colorCounts.Values.Sum();
            var threshold = totalSamples * 0.05; // 5% mínimo para considerar

            foreach (var color in colorCounts)
            {
                if (color.Value > threshold)
                {
                    detectedColors.Add(color.Key);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detectando colores: {ex.Message}");
        }

        return detectedColors.Distinct().Take(3).ToList(); // Máximo 3 colores
    }

    /// <summary>
    /// Determina el nombre del color basado en valores RGB
    /// </summary>
    private string? GetColorName(byte r, byte g, byte b)
    {
        foreach (var color in _colorRanges)
        {
            var range = color.Value;
            if (r >= range.rMin && r <= range.rMax &&
                g >= range.gMin && g <= range.gMax &&
                b >= range.bMin && b <= range.bMax)
            {
                return color.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// Versión simplificada para detectar color principal (sin muestreo)
    /// </summary>
    private string GetDominantColorSimple(string imagePath)
    {
        try
        {
            using var image = Image.Load<Rgb24>(imagePath);

            long totalR = 0, totalG = 0, totalB = 0;
            var pixels = 0;

            // Muestrear para rendimiento
            for (int y = 0; y < image.Height; y += 50)
            {
                for (int x = 0; x < image.Width; x += 50)
                {
                    var pixel = image[x, y];
                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                    pixels++;
                }
            }

            if (pixels == 0) return "desconocido";

            var avgR = totalR / pixels;
            var avgG = totalG / pixels;
            var avgB = totalB / pixels;

            // Determinar color dominante
            if (avgR > 200 && avgG < 100 && avgB < 100) return "rojo";
            if (avgR < 100 && avgG > 200 && avgB < 100) return "verde";
            if (avgR < 100 && avgG < 100 && avgB > 200) return "azul";
            if (avgR > 200 && avgG > 200 && avgB < 100) return "amarillo";
            if (avgR > 200 && avgG > 150 && avgB < 80) return "naranja";
            if (avgR > 150 && avgG < 100 && avgB > 150) return "morado";
            if (avgR > 200 && avgG < 150 && avgB > 150) return "rosa";
            if (avgR < 50 && avgG < 50 && avgB < 50) return "negro";
            if (avgR > 200 && avgG > 200 && avgB > 200) return "blanco";

            return "otro";
        }
        catch
        {
            return "desconocido";
        }
    }
}
