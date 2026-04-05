using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Woola.PhotoManager.Common.Services;

/// <summary>
/// D4: Evalúa nitidez (varianza de luminancia), exposición (brillo medio) y resolución.
/// Usa ImageSharp para muestrear píxeles sin cargar el ONNX runtime.
/// </summary>
public class QualityAssessmentService : IQualityAssessmentService
{
    public Task<QualityInfo> AssessAsync(string imagePath) =>
        Task.Run(() => Assess(imagePath));

    private static QualityInfo Assess(string imagePath)
    {
        try
        {
            using var image = Image.Load<Rgb24>(imagePath);
            int w = image.Width, h = image.Height;

            // Paso adaptativo: ~80 muestras por dimensión
            int step = Math.Max(4, Math.Min(w, h) / 80);

            float sumLum = 0f;
            float sumLumSq = 0f;
            int count = 0;

            for (int y = 0; y < h; y += step)
            {
                for (int x = 0; x < w; x += step)
                {
                    var p = image[x, y];
                    // Luminancia Rec. 709
                    float lum = 0.2126f * p.R + 0.7152f * p.G + 0.0722f * p.B;
                    sumLum += lum;
                    sumLumSq += lum * lum;
                    count++;
                }
            }

            if (count == 0) return new QualityInfo(0.5f, 0.5f, w, h);

            float avg = sumLum / count;
            float brightness = avg / 255f;

            // Varianza = E[X²] - E[X]²  (propiedad de König-Huygens)
            float variance = (sumLumSq / count) - (avg * avg);

            // Normalizar nitidez: varianza ~0 = sólido/borroso, ~3000+ = nítido
            float blurScore = Math.Min(variance / 3000f, 1.0f);

            return new QualityInfo(blurScore, brightness, w, h);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[QualityAssessmentService] Error en {imagePath}: {ex.Message}");
            return new QualityInfo(0.5f, 0.5f, 0, 0);
        }
    }
}
