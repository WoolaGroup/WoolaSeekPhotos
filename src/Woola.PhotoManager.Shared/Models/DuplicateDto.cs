namespace Woola.PhotoManager.Shared.Models;

public class DuplicateGroupDto
{
    public string Hash { get; set; } = string.Empty;
    public List<PhotoDto> Photos { get; set; } = new();
}

public class SimilarPhotoDto
{
    public PhotoDto Photo { get; set; } = new();
    public double Similarity { get; set; }
    public int CommonTags { get; set; }
    public string SimilarityPercent => $"{Similarity:P0}";
}
