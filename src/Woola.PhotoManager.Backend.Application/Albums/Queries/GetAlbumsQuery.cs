using MediatR;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.Application.Albums.Queries;

public record GetAlbumsQuery : IRequest<List<AlbumDto>>;

public class GetAlbumsHandler : IRequestHandler<GetAlbumsQuery, List<AlbumDto>>
{
    private readonly IAlbumRepository _albumRepo;

    public GetAlbumsHandler(IAlbumRepository albumRepo) => _albumRepo = albumRepo;

    public async Task<List<AlbumDto>> Handle(GetAlbumsQuery query, CancellationToken ct)
    {
        var albums = await _albumRepo.GetAllAsync(ct);
        return albums.Select(a => new AlbumDto
        {
            Id = a.Id,
            Name = a.Name,
            Description = a.Description,
            CoverPhotoId = a.CoverPhotoId,
            PhotoCount = a.AlbumPhotos?.Count ?? 0,
            CreatedAt = a.CreatedAt
        }).ToList();
    }
}
