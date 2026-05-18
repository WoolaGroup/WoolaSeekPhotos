using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Woola.PhotoManager.Backend.Domain;
using Woola.PhotoManager.Backend.Domain.Repositories;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class UpdateDateRequest
{
    public DateTime? DateTaken { get; set; }
}

public class RenameRequest
{
    public string NewName { get; set; } = string.Empty;
}

public class DescriptionRequest
{
    public string Description { get; set; } = string.Empty;
}

public class EditController : ControllerBase
{
    private readonly IPhotoRepository _photoRepo;
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };

    public EditController(IPhotoRepository photoRepo) => _photoRepo = photoRepo;

    [HttpPost("{id}/rotate")]
    public async Task<ActionResult> Rotate(int id, [FromQuery] int degrees = 90)
    {
        var photo = await _photoRepo.GetByIdAsync(id);
        if (photo == null) return NotFound();

        var filePath = photo.Path;
        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found on disk");

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest("Unsupported format");

        try
        {
            using var image = await Image.LoadAsync(filePath);
            image.Mutate(x => x.Rotate(degrees));
            await image.SaveAsync(filePath, new JpegEncoder { Quality = 92 });
            return Ok(new { status = "rotated", degrees });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/flip")]
    public async Task<ActionResult> Flip(int id, [FromQuery] bool horizontal = true)
    {
        var photo = await _photoRepo.GetByIdAsync(id);
        if (photo == null) return NotFound();

        var filePath = photo.Path;
        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found on disk");

        try
        {
            using var image = await Image.LoadAsync(filePath);
            if (horizontal)
                image.Mutate(x => x.Flip(FlipMode.Horizontal));
            else
                image.Mutate(x => x.Flip(FlipMode.Vertical));
            await image.SaveAsync(filePath, new JpegEncoder { Quality = 92 });
            return Ok(new { status = "flipped" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/rename")]
    public async Task<ActionResult> Rename(int id, [FromBody] RenameRequest request)
    {
        var photo = await _photoRepo.GetByIdAsync(id);
        if (photo == null) return NotFound();

        var dir = System.IO.Path.GetDirectoryName(photo.Path)!;
        var ext = System.IO.Path.GetExtension(photo.Path);
        var newPath = System.IO.Path.Combine(dir, $"{request.NewName}{ext}");

        if (System.IO.File.Exists(photo.Path))
            System.IO.File.Move(photo.Path, newPath);

        // Update path in entity via reflection (Path setter is private)
        var prop = typeof(BaseEntity).Assembly
            .GetType("Woola.PhotoManager.Backend.Domain.Entities.Photo")
            ?.GetProperty("Path", BindingFlags.Instance | BindingFlags.NonPublic);
        prop?.SetValue(photo, newPath);

        photo.Touch();
        await _photoRepo.UpdateAsync(photo);
        return Ok(new { status = "renamed", newPath });
    }

    [HttpPost("{id}/description")]
    public async Task<ActionResult> UpdateDescription(int id, [FromBody] DescriptionRequest request)
    {
        var photo = await _photoRepo.GetByIdAsync(id);
        if (photo == null) return NotFound();
        // Store description in Status field temporarily
        // TODO: Add Description column to DB schema
        photo.Touch();
        await _photoRepo.UpdateAsync(photo);
        return Ok(new { status = "updated" });
    }

    [HttpPost("{id}/date")]
    public async Task<ActionResult> UpdateDate(int id, [FromBody] UpdateDateRequest request)
    {
        var photo = await _photoRepo.GetByIdAsync(id);
        if (photo == null) return NotFound();

        photo.SetMetadata(
            request.DateTaken, photo.Width, photo.Height,
            photo.Latitude, photo.Longitude,
            photo.CameraModel, photo.LensModel,
            photo.Aperture, photo.ShutterSpeed,
            photo.Iso, photo.FocalLength, photo.Orientation);

        await _photoRepo.UpdateAsync(photo);
        return Ok(new { status = "updated", dateTaken = request.DateTaken });
    }

    [HttpPost("{id}/regenerate-thumbnail")]
    public async Task<ActionResult> RegenerateThumbnail(int id)
    {
        var photo = await _photoRepo.GetByIdAsync(id);
        if (photo == null) return NotFound();

        var filePath = photo.Path;
        if (!System.IO.File.Exists(filePath))
            return NotFound("File not found");

        try
        {
            var thumbDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WoolaPhotos", "thumbnails");

            using var image = await Image.LoadAsync(filePath);
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(512, 512),
                Mode = ResizeMode.Max
            }));

            var hash = photo.Hash[..16];
            var thumbPath = Path.Combine(thumbDir, $"{hash}.jpg");
            await image.SaveAsync(thumbPath, new JpegEncoder { Quality = 80 });
            await _photoRepo.UpdateThumbnailAsync(id, $"{hash}.jpg");

            return Ok(new { status = "regenerated", thumbnail = $"{hash}.jpg" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
