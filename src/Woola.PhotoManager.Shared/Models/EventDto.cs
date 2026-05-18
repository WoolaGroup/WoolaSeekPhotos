namespace Woola.PhotoManager.Shared.Models;

public class EventDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int PhotoCount { get; set; }
    public string Id => Start.ToString("yyyy-MM-dd");
    public string DateRange => Start == End
        ? Start.ToString("d MMM yyyy")
        : $"{Start:d MMM} - {End:d MMM yyyy}";
}

public class GeoPhotoDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? DateTaken { get; set; }
}
