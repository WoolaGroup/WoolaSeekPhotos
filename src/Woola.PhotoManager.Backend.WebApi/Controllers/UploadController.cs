using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Woola.PhotoManager.Backend.Domain.Entities;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<UploadController> _logger;

    public UploadController(IPhotoRepository photoRepo, ILogger<UploadController> logger)
    {
        _photoRepo = photoRepo;
        _logger = logger;
    }

    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<PhotoDto>> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp" };
        if (!allowed.Contains(ext))
            return BadRequest($"Format {ext} not supported");

        var photosDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "WoolaPhotos", "uploads");
        Directory.CreateDirectory(photosDir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(photosDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var fileInfo = new FileInfo(filePath);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        await using var fs = System.IO.File.OpenRead(filePath);
        var hashBytes = await sha256.ComputeHashAsync(fs);
        var hash = Convert.ToHexStringLower(hashBytes);

        var existing = await _photoRepo.GetByHashAsync(hash);
        if (existing != null)
        {
            System.IO.File.Delete(filePath);
            return Ok(new PhotoDto
            {
                Id = existing.Id, FileName = existing.FileName,
                Status = "duplicate_skipped"
            });
        }

        var photo = Photo.Create(filePath, hash, fileInfo.Length);

        using var image = await Image.LoadAsync(filePath);
        photo.SetMetadata(
            null, image.Width, image.Height,
            null, null, null, null, null, null, null, null, null);

        var photoId = await _photoRepo.InsertAsync(photo);

        var thumbDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WoolaPhotos", "thumbnails");
        Directory.CreateDirectory(thumbDir);

        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(512, 512), Mode = ResizeMode.Max
        }));
        var thumbName = $"{hash[..16]}.jpg";
        var thumbPath = Path.Combine(thumbDir, thumbName);
        await image.SaveAsync(thumbPath, new JpegEncoder { Quality = 80 });
        await _photoRepo.UpdateThumbnailAsync(photoId, thumbName);

        _logger.LogInformation("Uploaded {File} -> {Hash}", file.FileName, hash);

        return Ok(new PhotoDto
        {
            Id = photoId, FileName = file.FileName, Path = filePath,
            Hash = hash, FileSize = fileInfo.Length,
            Width = image.Width, Height = image.Height,
            ThumbnailUrl = $"/thumbnails/{thumbName}",
            CreatedAt = DateTime.UtcNow
        });
    }
}
