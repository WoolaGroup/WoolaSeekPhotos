using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Services;

public record HybridSearchResult(Photo Photo, float Score, int ExactMatches);

public interface IHybridSearchService
{
    Task<List<HybridSearchResult>> SearchAsync(string query, int limit = 50);
    void InvalidateCache();
}
