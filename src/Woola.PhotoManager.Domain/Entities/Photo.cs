namespace Woola.PhotoManager.Domain.Entities;

public class Photo
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long FileSize { get; set; }

    // Fechas
    public DateTime? DateTaken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastIndexedAt { get; set; }

    // Dimensiones
    public int Width { get; set; }
    public int Height { get; set; }

    // Metadata EXIF
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? CameraModel { get; set; }
    public string? LensModel { get; set; }
    public double? Aperture { get; set; }
    public double? ShutterSpeed { get; set; }
    public int? Iso { get; set; }
    public int? FocalLength { get; set; }
    public int? Orientation { get; set; }

    // Estado
    public string Status { get; set; } = "Discovered";
    public string? ThumbnailPath { get; set; }

    // Helpers
    public string FileName => System.IO.Path.GetFileName(Path);
    public string GpsText => (Latitude.HasValue && Longitude.HasValue)
        ? $"{Latitude:F6}, {Longitude:F6}"
        : "Sin GPS";
    public string CameraInfo => CameraModel ?? "Desconocida";
    public string ExposureInfo => $"{Aperture?.ToString("F1") ?? "?"}f / {ShutterSpeed?.ToString() ?? "?"}s / ISO {Iso?.ToString() ?? "?"}";
}