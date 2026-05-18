namespace Woola.PhotoManager.Backend.Application.Common.Interfaces;

public class AnalysisProgress
{
    public int TotalPending { get; set; }
    public int Processed { get; set; }
    public int CurrentPhotoId { get; set; }
    public string CurrentAction { get; set; } = string.Empty;
    public double Percentage => TotalPending > 0 ? Math.Round((double)Processed / TotalPending * 100, 1) : 0;
}

public class AnalysisResult
{
    public int PhotosAnalyzed { get; set; }
    public int TagsGenerated { get; set; }
    public int FacesDetected { get; set; }
    public long ElapsedMs { get; set; }
}

public interface IAnalysisService
{
    Task<AnalysisResult> AnalyzePendingPhotosAsync(
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken ct = default);
    Task<AnalysisResult> AnalyzePhotoAsync(int photoId, CancellationToken ct = default);
}
