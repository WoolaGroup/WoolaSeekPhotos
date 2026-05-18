using MediatR;
using Woola.PhotoManager.Backend.Application.Common.Interfaces;

namespace Woola.PhotoManager.Backend.Application.Photos.Commands;

public record IndexPhotosCommand(string FolderPath) : IRequest<IndexJobResult>;

public class IndexPhotosHandler : IRequestHandler<IndexPhotosCommand, IndexJobResult>
{
    private readonly IIndexingService _indexingService;

    public IndexPhotosHandler(IIndexingService indexingService) => _indexingService = indexingService;

    public async Task<IndexJobResult> Handle(IndexPhotosCommand command, CancellationToken ct)
    {
        var progress = new Progress<IndexProgress>();
        return await _indexingService.StartIndexingAsync(command.FolderPath, progress, ct);
    }
}
