namespace Woola.PhotoManager.Common.Services;

/// <summary>
/// Resultado del análisis de calidad de una imagen.
/// </summary>
/// <param name="BlurScore">0 = muy borrosa, 1 = muy nítida (basado en varianza de luminancia).</param>
/// <param name="Brightness">0 = negro puro, 1 = blanco puro (luminancia media normalizada).</param>
/// <param name="Width">Ancho en píxeles (0 si no se pudo leer).</param>
/// <param name="Height">Alto en píxeles (0 si no se pudo leer).</param>
public record QualityInfo(float BlurScore, float Brightness, int Width, int Height);

public interface IQualityAssessmentService
{
    /// <summary>
    /// Analiza la calidad de la imagen en la ruta indicada.
    /// Nunca lanza excepción: devuelve valores por defecto en caso de error.
    /// </summary>
    Task<QualityInfo> AssessAsync(string imagePath);
}
