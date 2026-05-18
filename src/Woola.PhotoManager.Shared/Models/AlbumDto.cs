namespace Woola.PhotoManager.Shared.Models;

public class AlbumDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CoverPhotoId { get; set; }
    public string? CoverThumbnailUrl { get; set; }
    public int PhotoCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAlbumRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateAlbumRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AddPhotosToAlbumRequest
{
    public List<int> PhotoIds { get; set; } = new();
}
