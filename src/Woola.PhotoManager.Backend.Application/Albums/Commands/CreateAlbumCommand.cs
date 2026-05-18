using MediatR;
using Woola.PhotoManager.Backend.Domain.Entities;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.Application.Albums.Commands;

public record CreateAlbumCommand(string Name, string? Description) : IRequest<AlbumDto>;

public class CreateAlbumHandler : IRequestHandler<CreateAlbumCommand, AlbumDto>
{
    private readonly IAlbumRepository _albumRepo;

    public CreateAlbumHandler(IAlbumRepository albumRepo) => _albumRepo = albumRepo;

    public async Task<AlbumDto> Handle(CreateAlbumCommand command, CancellationToken ct)
    {
        var album = Album.Create(command.Name, command.Description);
        var id = await _albumRepo.InsertAsync(album, ct);

        return new AlbumDto
        {
            Id = id,
            Name = album.Name,
            Description = album.Description,
            CreatedAt = album.CreatedAt
        };
    }
}
