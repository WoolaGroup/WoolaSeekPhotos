namespace Woola.PhotoManager.Shared.Models;

public class FaceDto
{
    public int Id { get; set; }
    public int PhotoId { get; set; }
    public string? PersonName { get; set; }
    public string? PersonId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Confidence { get; set; }
    public bool IsUserConfirmed { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FaceGroupDto
{
    public string? PersonName { get; set; }
    public int FaceCount { get; set; }
    public int PhotoCount { get; set; }
    public List<string> ThumbnailUrls { get; set; } = new();
}

public class RenameFaceRequest
{
    public string PersonName { get; set; } = string.Empty;
}
