using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.Application.Common.Mappings;

public static class MappingProfile
{
    public static PhotoDto ToDto(this Domain.Entities.Photo photo) => new()
    {
        Id = photo.Id,
        FileName = photo.FileName,
        Path = photo.Path,
        Hash = photo.Hash,
        FileSize = photo.FileSize,
        DateTaken = photo.DateTaken,
        Width = photo.Width,
        Height = photo.Height,
        Status = photo.Status,
        ThumbnailUrl = photo.ThumbnailPath != null
            ? $"/thumbnails/{photo.ThumbnailPath}"
            : null,
        CameraModel = photo.CameraModel,
        LensModel = photo.LensModel,
        Aperture = photo.Aperture,
        ShutterSpeed = photo.ShutterSpeed,
        Iso = photo.Iso,
        FocalLength = photo.FocalLength,
        Latitude = photo.Latitude,
        Longitude = photo.Longitude,
        CreatedAt = photo.CreatedAt
    };

    public static List<PhotoDto> ToDtoList(this IEnumerable<Domain.Entities.Photo> photos) =>
        photos.Select(ToDto).ToList();
}
