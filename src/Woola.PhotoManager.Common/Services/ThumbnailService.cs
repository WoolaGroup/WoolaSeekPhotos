using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Security.Cryptography;

namespace Woola.PhotoManager.Common.Services;

public class ThumbnailService : IThumbnailService
{
    private readonly string _cachePath;
    private const int MaxWidth = 512;
    private const int MaxHeight = 512;

    public ThumbnailService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cachePath = Path.Combine(appData, "Woola", "Thumbnails");
        Directory.CreateDirectory(_cachePath);
    }

    public async Task<string> GenerateThumbnailAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"Image not found: {imagePath}");

        var hash = ComputeFileHash(imagePath);
        var thumbnailPath = Path.Combine(_cachePath, $"{hash}.jpg");

        if (File.Exists(thumbnailPath))
            return thumbnailPath;

        using var image = await Image.LoadAsync(imagePath, cancellationToken);
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(MaxWidth, MaxHeight),
            Mode = ResizeMode.Max
        }));

        await image.SaveAsJpegAsync(thumbnailPath, cancellationToken);
        return thumbnailPath;
    }

    public Task<string?> GetExistingThumbnailAsync(string imagePath)
    {
        var hash = ComputeFileHash(imagePath);
        var thumbnailPath = Path.Combine(_cachePath, $"{hash}.jpg");
        return Task.FromResult(File.Exists(thumbnailPath) ? thumbnailPath : null);
    }

    public void ClearCache()
    {
        if (Directory.Exists(_cachePath))
        {
            foreach (var file in Directory.GetFiles(_cachePath))
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    private string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash)[..16];
    }
}
