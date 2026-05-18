using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Infrastructure.Data;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/smart-albums")]
public class SmartAlbumsController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public SmartAlbumsController(WoolaDbContext db) => _db = db;

    [HttpGet]
    public ActionResult<List<SmartAlbumDto>> GetAll()
    {
        return Ok(new List<SmartAlbumDto>
        {
            new() { Id = "no-album", Name = "Sin álbum", Icon = "📭", Description = "Fotos sin asignar a ningún álbum" },
            new() { Id = "recent", Name = "Recién añadidas", Icon = "🕐", Description = "Últimas 100 fotos añadidas" },
            new() { Id = "favorites", Name = "Favoritas", Icon = "⭐", Description = "Fotos marcadas como favoritas" },
            new() { Id = "no-location", Name = "Sin ubicación", Icon = "📍", Description = "Fotos sin coordenadas GPS" },
            new() { Id = "no-tags", Name = "Sin etiquetas", Icon = "🏷️", Description = "Fotos sin ningún tag asignado" },
        });
    }

    [HttpGet("{id}/photos")]
    public async Task<ActionResult<PagedApiResponse<PhotoDto>>> GetPhotos(
        string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        IQueryable<Domain.Entities.Photo> query = _db.Photos.AsNoTracking();

        query = id switch
        {
            "no-album" => query.Where(p => !p.PhotoTags.Any()),
            "recent" => query.OrderByDescending(p => p.CreatedAt),
            "favorites" => query.Where(p => p.Status == "Favorite"),
            "no-location" => query.Where(p => p.Latitude == null || p.Longitude == null),
            "no-tags" => query.Where(p => !p.PhotoTags.Any()),
            _ => query
        };

        if (id == "no-album")
            query = _db.Photos.AsNoTracking()
                .Where(p => !_db.AlbumPhotos.Any(ap => ap.PhotoId == p.Id));

        if (id != "recent")
            query = query.OrderByDescending(p => p.DateTaken ?? p.CreatedAt);

        var total = await query.CountAsync();
        var photos = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(PagedApiResponse<PhotoDto>.Ok(
            photos.Select(p => new PhotoDto
            {
                Id = p.Id,
                FileName = p.FileName,
                Path = p.Path,
                FileSize = p.FileSize,
                DateTaken = p.DateTaken,
                Width = p.Width,
                Height = p.Height,
                Status = p.Status,
                ThumbnailUrl = p.ThumbnailPath != null ? $"/thumbnails/{p.ThumbnailPath}" : null,
                CreatedAt = p.CreatedAt
            }).ToList(),
            total, page, pageSize));
    }

    [HttpPost("{id}/photos/{photoId}/toggle-favorite")]
    public async Task<ActionResult> ToggleFavorite(string id, int photoId)
    {
        if (id != "favorites") return BadRequest();

        var photo = await _db.Photos.FindAsync(photoId);
        if (photo == null) return NotFound();

        photo.Touch();
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

public class SmartAlbumDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PhotoCount { get; set; }
}
