namespace Woola.PhotoManager.Shared.Models;

public class DashboardStatsDto
{
    public int TotalPhotos { get; set; }
    public int TotalAlbums { get; set; }
    public int TotalFaces { get; set; }
    public int TotalTags { get; set; }
    public long TotalFileSizeBytes { get; set; }
    public int PhotosIndexedToday { get; set; }
    public int TotalPersons { get; set; }
    public string TotalFileSizeDisplay => TotalFileSizeBytes switch
    {
        < 1073741824 => $"{TotalFileSizeBytes / 1048576.0:F0} MB",
        _ => $"{TotalFileSizeBytes / 1073741824.0:F2} GB"
    };
}

public class MonthlyStatsDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Count { get; set; }
    public string Label => new DateTime(Year, Month, 1).ToString("MMM yyyy");
}

public class TopTagDto
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}
