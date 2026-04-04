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

        // Obtener todas las fotos
        var allPhotos = await _photoRepository.GetPhotosAsync(limit: 10000);

        foreach (var photo in allPhotos)
        {
            var score = 0f;

            // 1. Búsqueda por coincidencia exacta en tags (rápida y precisa)
            var photoTags = await _tagRepository.GetTagsForPhotoAsync(photo.Id);
            var tagNames = photoTags.Select(t => t.Name.ToLower()).ToList();

            foreach (var tag in tagNames)
            {
                if (tag.Contains(queryLower))
                    score += 0.8f;

                // Palabras clave relacionadas
                if (queryLower.Contains("niño") && (tag.Contains("persona") || tag.Contains("rostro")))
                    score += 0.5f;

                if (queryLower.Contains("rojo") && tag.Contains("rojo"))
                    score += 0.9f;

                if (queryLower.Contains("azul") && tag.Contains("azul"))
                    score += 0.9f;

                if (queryLower.Contains("perro") && tag.Contains("perro"))
                    score += 0.9f;

                if (queryLower.Contains("gato") && tag.Contains("gato"))
                    score += 0.9f;

                if (queryLower.Contains("auto") && (tag.Contains("auto") || tag.Contains("coche")))
                    score += 0.9f;
            }

            // 2. Búsqueda en nombre de archivo
            if (photo.FileName.ToLower().Contains(queryLower))
                score += 0.6f;

            // 3. Búsqueda semántica con embeddings (si está disponible)
            if (_embeddingService.IsAvailable && score < 0.5f)
            {
                var combinedText = string.Join(" ", tagNames) + " " + photo.FileName;
                var photoEmbedding = _embeddingService.GenerateEmbedding(combinedText);
                var queryEmbedding = _embeddingService.GenerateEmbedding(query);
                var semanticScore = _embeddingService.CosineSimilarity(queryEmbedding, photoEmbedding);
                score += semanticScore * 0.7f;
            }

            if (score > 0.1f)
            {
                results.Add((photo, Math.Min(score, 1.0f)));
            }
        }

        return results.OrderByDescending(r => r.score)
                      .Take(limit)
                      .Select(r => r.photo)
                      .ToList();
    }
}