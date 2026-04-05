namespace Woola.PhotoManager.Core.Services;

/// <summary>Resultado del proceso de clustering facial.</summary>
public record ClusterResult(int TotalFaces, int ClusterCount, int UpdatedFaces);

/// <summary>
/// IMP-002: Agrupa rostros almacenados en DB por similitud coseno de embeddings.
/// Los rostros confirmados por el usuario (IsUserConfirmed) conservan su asignación.
/// </summary>
public interface IFaceClusteringService
{
    /// <summary>
    /// Clusteriza todos los rostros con embedding disponible.
    /// threshold: similitud coseno mínima para agrupar (recomendado 0.60–0.70).
    /// </summary>
    Task<ClusterResult> ClusterAsync(float threshold = 0.65f);
}
