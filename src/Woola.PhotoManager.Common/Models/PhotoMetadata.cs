namespace Woola.PhotoManager.Common.Models;

public class PhotoMetadata
{
    public DateTime? DateTaken { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? CameraModel { get; set; }
    public string? LensModel { get; set; }
    public double? Aperture { get; set; }
    public double? ShutterSpeed { get; set; }
    public int? Iso { get; set; }
    public int? FocalLength { get; set; }
    public int? Orientation { get; set; }

    public bool HasGps => Latitude.HasValue && Longitude.HasValue;
    public string GpsText => HasGps ? $"{Latitude:F6}, {Longitude:F6}" : "Sin GPS";
}