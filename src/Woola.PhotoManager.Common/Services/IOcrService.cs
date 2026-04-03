using Woola.PhotoManager.Common.Models;

namespace Woola.PhotoManager.Common.Services;

public interface IOcrService
{
    Task<OcrResult> ExtractTextAsync(string imagePath);
    Task<bool> IsAvailable();
}

public class OcrResult
{
    public string Text { get; set; } = string.Empty;
    public List<OcrWord> Words { get; set; } = new();
    public double Confidence { get; set; }
    public bool Success { get; set; }
}

public class OcrWord
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
}