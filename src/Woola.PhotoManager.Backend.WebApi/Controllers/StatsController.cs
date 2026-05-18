using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class StatsController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public StatsController(WoolaDbContext db) => _db = db;

    [HttpGet("lenses")]
    public async Task<ActionResult> GetLensStats()
    {
        var stats = await _db.Photos
            .AsNoTracking()
            .Where(p => p.LensModel != null && p.LensModel != "")
            .GroupBy(p => p.LensModel)
            .Select(g => new { lens = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(20)
            .ToListAsync();

        return Ok(stats);
    }

    [HttpGet("cameras")]
    public async Task<ActionResult> GetCameraStats()
    {
        var stats = await _db.Photos
            .AsNoTracking()
            .Where(p => p.CameraModel != null && p.CameraModel != "")
            .GroupBy(p => p.CameraModel)
            .Select(g => new { camera = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(20)
            .ToListAsync();

        return Ok(stats);
    }

    [HttpGet("iso-distribution")]
    public async Task<ActionResult> GetIsoDistribution()
    {
        var stats = await _db.Photos
            .AsNoTracking()
            .Where(p => p.Iso != null)
            .GroupBy(p => p.Iso)
            .Select(g => new { iso = g.Key, count = g.Count() })
            .OrderBy(x => x.iso)
            .ToListAsync();

        return Ok(stats);
    }

    [HttpGet("storage-over-time")]
    public async Task<ActionResult> GetStorageByMonth()
    {
        var stats = await _db.Photos
            .AsNoTracking()
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                count = g.Count(),
                bytes = g.Sum(p => p.FileSize)
            })
            .OrderBy(x => x.year).ThenBy(x => x.month)
            .ToListAsync();

        return Ok(stats);
    }

    [HttpGet("yearly")]
    public async Task<ActionResult> GetYearlyBreakdown()
    {
        var stats = await _db.Photos
            .AsNoTracking()
            .Where(p => p.DateTaken != null)
            .GroupBy(p => p.DateTaken!.Value.Year)
            .Select(g => new { year = g.Key, count = g.Count() })
            .OrderByDescending(x => x.year)
            .ToListAsync();

        return Ok(stats);
    }
}
