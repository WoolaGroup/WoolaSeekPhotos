using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// IMP-009: Búsqueda de fotos similares usando embeddings de texto sobre tags.
///
/// Flujo:
///   1. Obtiene tags de la foto objetivo → genera embedding de texto.
///   2. Carga 2000 candidatos y sus tags (batch).
///   3. Para cada candidato: genera/recupera embedding → similitud coseno.
///   4. Retorna top-N ordenadas por similitud (excluye la foto original).
///
/// Cache: embeddings en memoria (invalidable). Se reconstruye lazy.
/// </summary>
public class SimilarPhotosService : ISimilarPhotosService
{
    private readonly PhotoRepository _photoRepository;
    private readonly TagRepository _tagRepository;
    private readonly TextEmbeddingService _embeddingService;

    // Cache: photoId → embedding float[]
    private readonly Dictionary<int, float[]> _embeddingCache = new();
    private readonly object _cacheLock = new();

    public SimilarPhotosService(PhotoRepository photoRepository,
                                 TagRepository tagRepository,
                                 TextEmbeddingService embeddingService)
    {
        _photoRepository  = photoRepository;
        _tagRepository    = tagRepository;
        _embeddingService = embeddingService;
    }

    public async Task<List<SimilarPhotoResult>> FindSimilarAsync(int photoId, int limit = 12)
    {
        if (!_embeddingService.IsAvailable)
            return [];

        // Embedding de la foto objetivo
        var targetTags = (await _tagRepository.GetTagsForPhotoAsync(photoId)).ToList();
        if (targetTags.Count == 0)
            return [];

        var targetText = BuildTagText(targetTags);
        var targetEmbedding = GetOrBuildEmbedding(photoId, targetText);

        // Candidatos: hasta 2000 fotos más recientes
        var candidates = (await _photoRepository.GetPhotosAsync(limit: 2000)).ToList();
        var candidateIds = candidates.Select(p => p.Id).ToList();

        // Tags de todos los candidatos en batch (1 query)
        var tagsBatch = await _tagRepository.GetTagsBatchAsync(candidateIds);

        var results = new List<SimilarPhotoResult>(candidates.Count);

        foreach (var photo in candidates)
        {
            if (photo.Id == photoId) continue;

            var photoTags = tagsBatch.GetValueOrDefault(photo.Id);
            if (photoTags == null || photoTags.Count == 0) continue;

            var text = BuildTagText(photoTags);
            var embedding = GetOrBuildEmbedding(photo.Id, text);

            var similarity = _embeddingService.CosineSimilarity(targetEmbedding, embedding);
            if (similarity > 0.30f)
                results.Add(new SimilarPhotoResult(photo, similarity));
        }

        return [.. results.OrderByDescending(r => r.Similarity).Take(limit)];
    }

    public void InvalidateCache()
    {
        lock (_cacheLock)
            _embeddingCache.Clear();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string BuildTagText(IEnumerable<Tag> tags)
        => string.Join(" ", tags.Select(t => t.Name));

    private float[] GetOrBuildEmbedding(int photoId, string text)
    {
        lock (_cacheLock)
        {
            if (_embeddingCache.TryGetValue(photoId, out var cached))
                return cached;

            var embedding = _embeddingService.GenerateEmbedding(text);
            _embeddingCache[photoId] = embedding;
            return embedding;
        }
    }
}
