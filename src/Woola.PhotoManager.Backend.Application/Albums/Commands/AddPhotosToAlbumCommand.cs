using MediatR;
using Woola.PhotoManager.Backend.Domain.Repositories;

namespace Woola.PhotoManager.Backend.Application.Albums.Commands;

public record AddPhotosToAlbumCommand(int AlbumId, List<int> PhotoIds) : IRequest<bool>;

public class AddPhotosToAlbumHandler : IRequestHandler<AddPhotosToAlbumCommand, bool>
{
    private readonly IAlbumRepository _albumRepo;

    public AddPhotosToAlbumHandler(IAlbumRepository albumRepo) => _albumRepo = albumRepo;

    public async Task<bool> Handle(AddPhotosToAlbumCommand command, CancellationToken ct)
    {
        foreach (var photoId in command.PhotoIds)
        {
            var exists = await _albumRepo.HasPhotoAsync(command.AlbumId, photoId, ct);
            if (!exists)
                await _albumRepo.AddPhotoAsync(command.AlbumId, photoId, ct);
        }
        return true;
    }
}
