using MediatR;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.Application.Photos.Queries;

public record GetPhotosQuery(
    int Page = 1,
    int PageSize = 50,
    int? AlbumId = null,
    string? Tag = null,
    string? Search = null,
    string? SortBy = "dateTaken",
    string? SortDir = "desc"
) : IRequest<PagedApiResponse<PhotoDto>>;

public class GetPhotosHandler : IRequestHandler<GetPhotosQuery, PagedApiResponse<PhotoDto>>
{
    private readonly IPhotoRepository _photoRepo;
    private readonly IAlbumRepository _albumRepo;
    private readonly ITagRepository _tagRepo;

    public GetPhotosHandler(
        IPhotoRepository photoRepo,
        IAlbumRepository albumRepo,
        ITagRepository tagRepo)
    {
        _photoRepo = photoRepo;
        _albumRepo = albumRepo;
        _tagRepo = tagRepo;
    }

    public async Task<PagedApiResponse<PhotoDto>> Handle(
        GetPhotosQuery query, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(query.Search))
        {
            var result = await _photoRepo.SearchAsync(query.Search, query.Page, query.PageSize, ct);
            return PagedApiResponse<PhotoDto>.Ok(
                result.Items.Select(MapToDto).ToList(),
                result.TotalCount, query.Page, query.PageSize);
        }

        if (query.AlbumId.HasValue)
        {
            var result = await _albumRepo.GetPhotosAsync(query.AlbumId.Value, query.Page, query.PageSize, ct);
            return PagedApiResponse<PhotoDto>.Ok(
                result.Items.Select(MapToDto).ToList(),
                result.TotalCount, query.Page, query.PageSize);
        }

        if (!string.IsNullOrEmpty(query.Tag))
        {
            var result = await _tagRepo.GetPhotosAsync(new[] { query.Tag }, query.Page, query.PageSize, ct);
            return PagedApiResponse<PhotoDto>.Ok(
                result.Items.Select(MapToDto).ToList(),
                result.TotalCount, query.Page, query.PageSize);
        }

        var allResult = await _photoRepo.GetPhotosAsync(
            query.Page, query.PageSize, query.SortBy, query.SortDir, ct);

        return PagedApiResponse<PhotoDto>.Ok(
            allResult.Items.Select(MapToDto).ToList(),
            allResult.TotalCount, query.Page, query.PageSize);
    }

    private static PhotoDto MapToDto(Domain.Entities.Photo photo) => new()
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
}
