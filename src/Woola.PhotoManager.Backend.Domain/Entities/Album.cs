namespace Woola.PhotoManager.Backend.Domain.Entities;

public class Album : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int? CoverPhotoId { get; private set; }

    public Photo? CoverPhoto { get; private set; }
    public ICollection<AlbumPhoto> AlbumPhotos { get; private set; } = new List<AlbumPhoto>();

    public static Album Create(string name, string? description)
    {
        return new Album
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
        Touch();
    }

    public void SetCover(int photoId)
    {
        CoverPhotoId = photoId;
        Touch();
    }
}
