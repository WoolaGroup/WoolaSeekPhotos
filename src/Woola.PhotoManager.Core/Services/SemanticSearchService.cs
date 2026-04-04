using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

public interface ISemanticSearchService
{
    Task<List<Photo>> SearchAsync(string query, int limit = 50);
}

public class SemanticSearchService : ISemanticSearchService
{
    private readonly PhotoRepository _photoRepository;
    private readonly TagRepository _tagRepository;
    private readonly TextEmbeddingService _embeddingService;

    public SemanticSearchService(PhotoRepository photoRepository, TagRepository tagRepository, TextEmbeddingService embeddingService)
    {
        _photoRepository = photoRepository;
        _tagRepository = tagRepository;
        _embeddingService = embeddingService;
    }

    public async Task<List<Photo>> SearchAsync(string query, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<Photo>();

        var queryLower = query.ToLower();
        var results = new List<(Photo photo, float score)>();
        float[]? queryEmbedding = null;

        if (_embeddingService.IsAvailable)
            queryEmbedding = _embeddingService.GenerateEmbedding(query);

        var allPhotos = await _photoRepository.GetPhotosAsync(limit: 10000);

        foreach (var photo in allPhotos)
        {
            var score = 0f;

            // 1. Coincidencia en tags ponderada por confianza real del agente
            var photoTags = await _tagRepository.GetTagsForPhotoAsync(photo.Id);
            var tagList = photoTags.ToList();
            var tagNames = tagList.Select(t => t.Name.ToLower()).ToList();

            foreach (var tag in tagList)
            {
                var tagLower = tag.Name.ToLower();
                if (tagLower.Contains(queryLower) || queryLower.Contains(tagLower))
                    score += 0.8f * tag.Confidence;
            }

            // 2. Coincidencia en nombre de archivo
            if (photo.FileName.ToLower().Contains(queryLower))
                score += 0.4f;

            // 3. Búsqueda semántica con embeddings (siempre, no solo como fallback)
            if (queryEmbedding != null && tagNames.Count > 0)
            {
                var combinedText = string.Join(" ", tagNames) + " " + photo.FileName;
                var photoEmbedding = _embeddingService.GenerateEmbedding(combinedText);
                var semanticScore = _embeddingService.CosineSimilarity(queryEmbedding, photoEmbedding);
                // Peso semántico mayor si hay pocos tags exactos
                var semanticWeight = score < 0.3f ? 0.7f : 0.3f;
                score += semanticScore * semanticWeight;
            }

            if (score > 0.1f)
                results.Add((photo, Math.Min(score, 1.0f)));
        }

        return results.OrderByDescending(r => r.score)
                      .Take(limit)
                      .Select(r => r.photo)
                      .ToList();
    }
}