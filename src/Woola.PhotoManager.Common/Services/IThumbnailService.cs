using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Woola.PhotoManager.Common.Services;

public interface IThumbnailService
{
    Task<string> GenerateThumbnailAsync(string imagePath, CancellationToken cancellationToken = default);
    Task<string?> GetExistingThumbnailAsync(string imagePath);
    void ClearCache();
}
