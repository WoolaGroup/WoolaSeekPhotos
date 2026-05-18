namespace Woola.PhotoManager.Shared.Models;

public class PhotoDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime? DateTaken { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Status { get; set; } = "Discovered";
    public string? ThumbnailUrl { get; set; }
    public string? CameraModel { get; set; }
    public string? LensModel { get; set; }
    public double? Aperture { get; set; }
    public double? ShutterSpeed { get; set; }
    public int? Iso { get; set; }
    public int? FocalLength { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Resolution => $"{Width}x{Height}";
    public string FileSizeDisplay => FileSize switch
    {
        < 1024 => $"{FileSize} B",
        < 1048576 => $"{FileSize / 1024.0:F0} KB",
        _ => $"{FileSize / 1048576.0:F1} MB"
    };
}

public class PhotoDetailDto : PhotoDto
{
    public List<TagDto> Tags { get; set; } = new();
    public List<FaceDto> Faces { get; set; } = new();
    public List<AlbumDto> Albums { get; set; } = new();
}

public class PhotoUploadDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
