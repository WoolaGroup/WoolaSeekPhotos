using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Domain;
using Woola.PhotoManager.Backend.Infrastructure.Data;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TrashController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public TrashController(WoolaDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<PhotoDto>>> GetAll()
    {
        var photos = await _db.Photos
            .IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Select(p => new PhotoDto
            {
                Id = p.Id,
                FileName = p.FileName,
                Path = p.Path,
                FileSize = p.FileSize,
                DateTaken = p.DateTaken,
                ThumbnailUrl = p.ThumbnailPath != null ? $"/thumbnails/{p.ThumbnailPath}" : null,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(photos);
    }

    [HttpPost("{id}/restore")]
    public async Task<ActionResult> Restore(int id)
    {
        var photo = await _db.Photos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted);
        if (photo == null) return NotFound();

        typeof(BaseEntity).GetMethod("SoftDelete")?.Invoke(photo, null);
        var prop = typeof(BaseEntity).GetProperty("IsDeleted");
        if (prop?.CanWrite == true)
            prop.SetValue(photo, false);

        await _db.SaveChangesAsync();
        return Ok(new { status = "restored" });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> PermanentDelete(int id)
    {
        var photo = await _db.Photos.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id && p.IsDeleted);
        if (photo == null) return NotFound();

        if (System.IO.File.Exists(photo.Path))
            System.IO.File.Delete(photo.Path);
        if (photo.ThumbnailPath != null)
        {
            var thumbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WoolaPhotos", "thumbnails", photo.ThumbnailPath);
            if (System.IO.File.Exists(thumbPath))
                System.IO.File.Delete(thumbPath);
        }

        _db.Photos.Remove(photo);
        await _db.SaveChangesAsync();
        return Ok(new { status = "permanently_deleted" });
    }

    [HttpPost("empty")]
    public async Task<ActionResult> EmptyTrash()
    {
        var deleted = await _db.Photos.IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .CountAsync();

        await _db.Photos.IgnoreQueryFilters()
            .Where(p => p.IsDeleted)
            .ExecuteDeleteAsync();

        return Ok(new { deleted });
    }
}
