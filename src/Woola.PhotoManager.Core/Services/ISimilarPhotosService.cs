using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Services;

/// <summary>IMP-009: Resultado de búsqueda de fotos similares.</summary>
public record SimilarPhotoResult(Photo Photo, float Similarity);

/// <summary>IMP-009: Encuentra fotos similares a una dada usando embeddings de tags.</summary>
public interface ISimilarPhotosService
{
    /// <summary>
    /// Retorna las <paramref name="limit"/> fotos más similares a la foto con <paramref name="photoId"/>,
    /// usando similitud coseno sobre embeddings de texto generados desde los tags.
    /// Retorna lista vacía si TextEmbeddingService no está disponible.
    /// </summary>
    Task<List<SimilarPhotoResult>> FindSimilarAsync(int photoId, int limit = 12);

    /// <summary>Invalida el cache de embeddings (llamar después de reindexar).</summary>
    void InvalidateCache();
}
