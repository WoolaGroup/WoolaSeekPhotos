using Woola.PhotoManager.Backend.Domain.ValueObjects;

namespace Woola.PhotoManager.Backend.Domain.Entities;

public class Photo : BaseEntity
{
    public string Path { get; private set; } = string.Empty;
    public string Hash { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public DateTime? DateTaken { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string Status { get; private set; } = "Discovered";
    public string? ThumbnailPath { get; private set; }
    public DateTime? LastIndexedAt { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public string? CameraModel { get; private set; }
    public string? LensModel { get; private set; }
    public double? Aperture { get; private set; }
    public double? ShutterSpeed { get; private set; }
    public int? Iso { get; private set; }
    public int? FocalLength { get; private set; }
    public int? Orientation { get; private set; }
    public int Rating { get; private set; }

    public ICollection<Face> Faces { get; private set; } = new List<Face>();
    public ICollection<PhotoTag> PhotoTags { get; private set; } = new List<PhotoTag>();

    public string FileName => System.IO.Path.GetFileName(Path);

    public GeoLocation GeoLocation => new(Latitude, Longitude);

    public ExifData ExifData => new(CameraModel, LensModel, Aperture, Iso, FocalLength, ShutterSpeed, Orientation);

    public static Photo Create(string path, string hash, long fileSize)
    {
        return new Photo
        {
            Path = path,
            Hash = hash,
            FileSize = fileSize,
            Status = "Discovered",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetThumbnail(string thumbnailPath)
    {
        ThumbnailPath = thumbnailPath;
        Touch();
    }

    public void SetMetadata(
        DateTime? dateTaken, int width, int height,
        double? latitude, double? longitude,
        string? cameraModel, string? lensModel,
        double? aperture, double? shutterSpeed,
        int? iso, int? focalLength, int? orientation)
    {
        DateTaken = dateTaken;
        Width = width;
        Height = height;
        Latitude = latitude;
        Longitude = longitude;
        CameraModel = cameraModel;
        LensModel = lensModel;
        Aperture = aperture;
        ShutterSpeed = shutterSpeed;
        Iso = iso;
        FocalLength = focalLength;
        Orientation = orientation;
        Touch();
    }

    public void MarkIndexed()
    {
        Status = "Indexed";
        LastIndexedAt = DateTime.UtcNow;
        Touch();
    }
}
