namespace Woola.PhotoManager.Backend.Application.Common.Interfaces;

public class IndexProgress
{
    public int TotalFound { get; set; }
    public int Processed { get; set; }
    public double Percentage => TotalFound > 0 ? Math.Round((double)Processed / TotalFound * 100, 1) : 0;
    public string CurrentFile { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long ElapsedMs { get; set; }
}

public class IndexJobResult
{
    public int TotalPhotos { get; set; }
    public int NewPhotos { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int Errors { get; set; }
    public long ElapsedMs { get; set; }
    public string Status { get; set; } = string.Empty;
}

public interface IIndexingService
{
    Task<IndexJobResult> StartIndexingAsync(string folderPath,
        IProgress<IndexProgress>? progress = null,
        CancellationToken ct = default);
    Task CancelIndexingAsync();
    bool IsIndexing { get; }
}
