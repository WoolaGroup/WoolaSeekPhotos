namespace Woola.PhotoManager.Backend.Domain.ValueObjects;

public record ExifData(
    string? CameraModel,
    string? LensModel,
    double? Aperture,
    int? Iso,
    int? FocalLength,
    double? ShutterSpeed,
    int? Orientation)
{
    public string ApertureDisplay => Aperture.HasValue ? $"f/{Aperture:F1}" : "N/A";
    public string IsoDisplay => Iso.HasValue ? $"ISO {Iso}" : "N/A";
    public string FocalLengthDisplay => FocalLength.HasValue ? $"{FocalLength}mm" : "N/A";
}
