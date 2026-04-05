namespace Woola.PhotoManager.Domain.Entities;

public class Album
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? CoverPhotoId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Poblados por queries con JOINs — no existen en la tabla Albums
    public int PhotoCount { get; set; }
    public string? CoverThumbnailPath { get; set; }
}
