using Woola.PhotoManager.Common.Models;

namespace Woola.PhotoManager.Common.Services;

public interface IObjectDetectionService
{
    Task<List<DetectedObject>> DetectObjectsAsync(string imagePath);
    Task<bool> IsModelAvailable();
    Task DownloadModelIfNeededAsync();
}

public class DetectedObject
{
    public string ClassName { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}