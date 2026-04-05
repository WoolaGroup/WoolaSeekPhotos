using System.Collections.Concurrent;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

public class HybridSearchService : IHybridSearchService
{
    private readonly PhotoRepository _photoRepository;
    private readonly TagRepository _tagRepository;
    private readonly TextEmbeddingService _embeddingService;

    // F4: TTL cache — avoids re-running expensive prefilter + embedding for repeated queries
    private readonly ConcurrentDictionary<string, (DateTime ExpiresAt, List<HybridSearchResult> Results)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public HybridSearchService(PhotoRepository photoRepository, TagRepository tagRepository,
        TextEmbeddingService embeddingService)
    {
        _photoRepository = photoRepository;
        _tagRepository = tagRepository;
        _embeddingService = embeddingService;
    }

    public async Task<List<HybridSearchResult>> SearchAsync(string query, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var queryLower = query.Trim().ToLowerInvariant();

        // F4: Return cached result if still fresh
        var cacheKey = $"{queryLower}:{limit}";
        if (_cache.TryGetValue(cacheKey, out var entry) && DateTime.UtcNow < entry.ExpiresAt)
            return entry.Results;

        // Paso 1: prefilter SQL (≤500 candidatos, rápido)
        var candidates = await _photoRepository.SearchCandidatesAsync(queryLower, limit: 500);
        if (candidates.Count == 0) return [];

        // Paso 2: tags en batch (1 query para todos los candidatos)
        var tagsByPhoto = await _tagRepository.GetTagsBatchAsync(candidates.Select(p => p.Id));

        // Paso 3: embedding del query (graceful fallback si modelo no disponible)
        float[]? queryEmbedding = _embeddingService.IsAvailable
            ? _embeddingService.GenerateEmbedding(query)
            : null;

        var results = new List<HybridSearchResult>();
        foreach (var photo in candidates)
        {
            var tags = tagsByPhoto.GetValueOrDefault(photo.Id, []);
            var score = 0f;
            var exactMatches = 0;

            foreach (var tag in tags)
            {
                var tagLower = tag.Name.ToLowerInvariant();
                if (tagLower == queryLower)
                {
                    score += 1.0f * (float)tag.Confidence;
                    exactMatches++;
                }
                else if (tagLower.Contains(queryLower) || queryLower.Contains(tagLower))
                    score += 0.6f * (float)tag.Confidence;
            }

            var fileName = System.IO.Path.GetFileNameWithoutExtension(photo.Path).ToLowerInvariant();
            if (fileName.Contains(queryLower)) score += 0.4f;

            if (queryEmbedding != null && tags.Count > 0)
            {
                var tagText = string.Join(" ", tags.Select(t => t.Name)) + " " + fileName;
                var photoEmbedding = _embeddingService.GenerateEmbedding(tagText);
                var semanticScore = _embeddingService.CosineSimilarity(queryEmbedding, photoEmbedding);
                score += semanticScore * (score < 0.3f ? 0.6f : 0.2f);
            }

            if (score > 0.05f)
                results.Add(new HybridSearchResult(photo, Math.Min(score, 2.0f), exactMatches));
        }

        var sorted = results.OrderByDescending(r => r.Score).Take(limit).ToList();

        // F4: Store in cache with TTL
        _cache[cacheKey] = (DateTime.UtcNow.Add(CacheTtl), sorted);

        return sorted;
    }

    public void InvalidateCache() => _cache.Clear();
}
