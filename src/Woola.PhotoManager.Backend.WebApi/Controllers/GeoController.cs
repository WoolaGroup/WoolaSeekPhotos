using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Infrastructure.Data;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class GeoController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public GeoController(WoolaDbContext db) => _db = db;

    [HttpGet("photos")]
    public async Task<ActionResult<List<GeoPhotoDto>>> GetPhotos()
    {
        var photos = await _db.Photos
            .AsNoTracking()
            .Where(p => p.Latitude != null && p.Longitude != null)
            .Select(p => new GeoPhotoDto
            {
                Id = p.Id,
                FileName = p.FileName,
                Latitude = p.Latitude!.Value,
                Longitude = p.Longitude!.Value,
                ThumbnailUrl = p.ThumbnailPath != null ? $"/thumbnails/{p.ThumbnailPath}" : null,
                DateTaken = p.DateTaken
            })
            .ToListAsync();

        return Ok(photos);
    }
}

public class GeoPhotoDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? DateTaken { get; set; }
}
