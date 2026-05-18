namespace Woola.PhotoManager.Backend.Domain.Entities;

public class PhotoTag
{
    public int PhotoId { get; private set; }
    public int TagId { get; private set; }
    public double Confidence { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    public Photo? Photo { get; private set; }
    public Tag? Tag { get; private set; }

    public static PhotoTag Create(int photoId, int tagId, double confidence, string source)
    {
        return new PhotoTag
        {
            PhotoId = photoId,
            TagId = tagId,
            Confidence = confidence,
            Source = source,
            CreatedAt = DateTime.UtcNow
        };
    }
}
