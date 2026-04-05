using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Agents.Agents;

/// <summary>
/// D2: Infiere escenas (interior, exterior, retrato, comida, deporte, noche, etc.)
/// a partir de los tags ya guardados por agentes anteriores (VisionAgent P3, OcrAgent P4)
/// más un análisis rápido de brillo para detectar noche.
/// Prioridad 6 — corre DESPUÉS de Vision y OCR.
/// </summary>
public class SceneAgent : IAgent
{
    private readonly TagRepository _tagRepository;

    public string Name => "SceneAgent";
    public string Description => "Infiere escenas a partir de objetos detectados y brillo";
    public int Priority => 6;
    public bool IsEnabled { get; set; } = true;

    // ── Grupos de nombres de clase COCO (inglés, tal como los genera ObjectDetectionService) ──

    private static readonly HashSet<string> _animalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bird", "cat", "dog", "horse", "sheep", "cow",
        "elephant", "bear", "zebra", "giraffe"
    };

    private static readonly HashSet<string> _foodNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "banana", "apple", "sandwich", "orange", "broccoli", "carrot",
        "hot dog", "pizza", "donut", "cake", "bowl", "cup", "wine glass", "bottle"
    };

    private static readonly HashSet<string> _sportsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "sports ball", "kite", "baseball bat", "baseball glove",
        "skateboard", "surfboard", "tennis racket", "skis", "snowboard", "frisbee"
    };

    private static readonly HashSet<string> _interiorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chair", "sofa", "bed", "diningtable", "toilet", "tvmonitor",
        "laptop", "mouse", "keyboard", "remote", "cell phone",
        "microwave", "oven", "toaster", "sink", "refrigerator",
        "pottedplant", "clock", "vase", "book"
    };

    private static readonly HashSet<string> _exteriorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "car", "bicycle", "motorbike", "bus", "truck", "train",
        "aeroplane", "boat", "bench", "traffic light", "fire hydrant", "stop sign"
    };

    private static readonly HashSet<string> _travelNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "aeroplane", "boat", "train", "bus", "backpack", "suitcase"
    };

    // ─────────────────────────────────────────────────────────────────────────

    public SceneAgent(TagRepository tagRepository)
    {
        _tagRepository = tagRepository;
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
            // Leer tags ya guardados por agentes anteriores (Vision P3, OCR P4)
            var existingTags = (await _tagRepository.GetTagsForPhotoAsync(photo.Id)).ToList();

            var objectTags = existingTags.Where(t => t.Category == "Object").ToList();
            var featureNames = existingTags
                .Where(t => t.Category == "Feature")
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var hasDocumentTag = existingTags.Any(t => t.Category == "Document");

            // Análisis rápido de brillo para detectar noche
            float brightness = EstimateBrightness(photo.Path);

            InferScenes(objectTags, featureNames, hasDocumentTag, brightness, result.Tags);

            result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────

    private static void InferScenes(
        List<Tag> objectTags,
        HashSet<string> featureNames,
        bool hasDocumentTag,
        float brightness,
        List<AgentTag> output)
    {
        // Retrato / grupo de personas
        var personTags = objectTags.Where(t => t.Name == "person").ToList();
        if (personTags.Count > 0)
        {
            double maxConf = personTags.Max(t => (double)t.Confidence);
            output.Add(personTags.Count >= 3
                ? Scene("escena_grupo", Math.Min(maxConf * 1.1, 1.0))
                : Scene("escena_retrato", maxConf));
        }

        // Animales
        var animalMatches = objectTags.Where(t => _animalNames.Contains(t.Name)).ToList();
        if (animalMatches.Count > 0)
            output.Add(Scene("escena_animal", animalMatches.Max(t => (double)t.Confidence)));

        // Comida
        var foodMatches = objectTags.Where(t => _foodNames.Contains(t.Name)).ToList();
        if (foodMatches.Count > 0)
            output.Add(Scene("escena_comida", foodMatches.Max(t => (double)t.Confidence)));

        // Deporte
        var sportsMatches = objectTags.Where(t => _sportsNames.Contains(t.Name)).ToList();
        if (sportsMatches.Count > 0)
            output.Add(Scene("escena_deporte", sportsMatches.Max(t => (double)t.Confidence)));

        // Interior (≥2 objetos domésticos = más confianza)
        var interiorMatches = objectTags.Where(t => _interiorNames.Contains(t.Name)).ToList();
        if (interiorMatches.Count >= 2)
            output.Add(Scene("escena_interior",
                Math.Min(interiorMatches.Average(t => (double)t.Confidence) * 1.2, 1.0)));
        else if (interiorMatches.Count == 1)
            output.Add(Scene("escena_interior", (double)interiorMatches[0].Confidence));

        // Exterior (≥2 objetos de calle, y sin señal fuerte de interior)
        var exteriorMatches = objectTags.Where(t => _exteriorNames.Contains(t.Name)).ToList();
        if (exteriorMatches.Count >= 2 && interiorMatches.Count < 2)
            output.Add(Scene("escena_exterior",
                Math.Min(exteriorMatches.Average(t => (double)t.Confidence) * 1.1, 1.0)));

        // Viaje
        var travelMatches = objectTags.Where(t => _travelNames.Contains(t.Name)).ToList();
        if (travelMatches.Count > 0)
            output.Add(Scene("escena_viaje", travelMatches.Max(t => (double)t.Confidence)));

        // Documento (OcrAgent detectó tipo de documento o texto presente)
        if (hasDocumentTag || featureNames.Contains("contiene_texto"))
            output.Add(Scene("escena_documento", 0.8));

        // Noche (brillo < 20%)
        if (brightness < 0.20f)
            output.Add(Scene("escena_noche", Math.Min(1.0 - brightness * 4.0, 0.95)));
    }

    private static AgentTag Scene(string name, double confidence) => new AgentTag
    {
        Name = name,
        Category = "Scene",
        Confidence = confidence,
        Source = "SceneAgent"
    };

    /// <summary>
    /// Muestreo rápido de brillo promedio (luminancia Rec.709 normalizada 0-1).
    /// </summary>
    private static float EstimateBrightness(string imagePath)
    {
        try
        {
            using var image = Image.Load<Rgb24>(imagePath);
            int step = Math.Max(10, Math.Min(image.Width, image.Height) / 50);
            float sum = 0f;
            int count = 0;
            for (int y = 0; y < image.Height; y += step)
                for (int x = 0; x < image.Width; x += step)
                {
                    var p = image[x, y];
                    sum += 0.2126f * p.R + 0.7152f * p.G + 0.0722f * p.B;
                    count++;
                }
            return count > 0 ? sum / count / 255f : 0.5f;
        }
        catch
        {
            return 0.5f;
        }
    }
}
