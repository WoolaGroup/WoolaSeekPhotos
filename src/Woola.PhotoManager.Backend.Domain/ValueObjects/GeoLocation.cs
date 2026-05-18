namespace Woola.PhotoManager.Backend.Domain.ValueObjects;

public record GeoLocation(double? Latitude, double? Longitude)
{
    public bool HasValue => Latitude.HasValue && Longitude.HasValue;

    public string ToDisplayString() =>
        HasValue ? $"{Latitude:F5}, {Longitude:F5}" : "Sin ubicación";
}
