namespace Woola.PhotoManager.Shared.Models;

public class SmartAlbumDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PhotoCount { get; set; }
}
