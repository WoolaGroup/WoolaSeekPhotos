using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Database;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

public interface IAutoTaggingService
{
    Task<List<string>> GenerateTagsForPhotoAsync(Photo photo);
    Task ApplyTagsToPhotoAsync(int photoId, List<string> tags);
    Task UpdateTagsForExistingPhotoAsync(int photoId, Photo photo);
}