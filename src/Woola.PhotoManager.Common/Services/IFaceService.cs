using Woola.PhotoManager.Common.Models;

namespace Woola.PhotoManager.Common.Services;

public interface IFaceService
{
    Task<List<DetectedFace>> DetectFacesAsync(string imagePath);
    Task<float[]> GenerateEmbeddingAsync(string imagePath, DetectedFace face);
    Task<bool> IsModelAvailable();
    Task DownloadModelsIfNeededAsync();
}

public class DetectedFace
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Confidence { get; set; }
    public float[]? Embedding { get; set; }
}