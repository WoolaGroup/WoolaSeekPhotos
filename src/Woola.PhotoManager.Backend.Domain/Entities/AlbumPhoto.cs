namespace Woola.PhotoManager.Backend.Domain.Entities;

public class AlbumPhoto
{
    public int AlbumId { get; private set; }
    public int PhotoId { get; private set; }
    public DateTime AddedAt { get; private set; } = DateTime.UtcNow;
    public int SortOrder { get; private set; }

    public Album? Album { get; private set; }
    public Photo? Photo { get; private set; }

    public static AlbumPhoto Create(int albumId, int photoId, int sortOrder = 0)
    {
        return new AlbumPhoto
        {
            AlbumId = albumId,
            PhotoId = photoId,
            SortOrder = sortOrder,
            AddedAt = DateTime.UtcNow
        };
    }

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
}
