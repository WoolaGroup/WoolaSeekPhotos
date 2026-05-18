using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.Application.Common.Interfaces;

public class SearchResult
{
    public PhotoDto Photo { get; set; } = new();
    public double Score { get; set; }
    public int ExactMatches { get; set; }
    public List<string> MatchedTags { get; set; } = new();
}

public interface ISearchService
{
    Task<List<SearchResult>> HybridSearchAsync(string query, int limit = 200, CancellationToken ct = default);
    Task InvalidateCache();
}
