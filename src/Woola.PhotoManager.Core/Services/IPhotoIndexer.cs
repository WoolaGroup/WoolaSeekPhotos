namespace Woola.PhotoManager.Core.Services;

public interface IPhotoIndexer
{
    Task StartIndexingAsync(string rootPath, CancellationToken cancellationToken = default);
    Task StopIndexingAsync();
    bool IsRunning { get; }
    event EventHandler<IndexingProgress>? ProgressChanged;
}

public class IndexingProgress : EventArgs
{
    public int TotalFound { get; set; }
    public int Processed { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public TimeSpan Elapsed { get; set; }
    public double Percentage => TotalFound > 0 ? (double)Processed / TotalFound * 100 : 0;
}