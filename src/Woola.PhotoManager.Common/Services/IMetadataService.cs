using Woola.PhotoManager.Common.Models;

namespace Woola.PhotoManager.Common.Services;

public interface IMetadataService
{
    Task<PhotoMetadata> ExtractMetadataAsync(string imagePath);
}