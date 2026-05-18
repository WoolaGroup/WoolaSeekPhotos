using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Backend.Infrastructure.Data;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IPhotoRepository _photoRepo;
    private readonly IAlbumRepository _albumRepo;
    private readonly IFaceRepository _faceRepo;
    private readonly ITagRepository _tagRepo;
    private readonly WoolaDbContext _db;

    public DashboardController(
        IPhotoRepository photoRepo,
        IAlbumRepository albumRepo,
        IFaceRepository faceRepo,
        ITagRepository tagRepo,
        WoolaDbContext db)
    {
        _photoRepo = photoRepo;
        _albumRepo = albumRepo;
        _faceRepo = faceRepo;
        _tagRepo = tagRepo;
        _db = db;
    }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var totalPhotos = await _photoRepo.GetTotalCountAsync();
        var totalAlbums = await _albumRepo.GetTotalCountAsync();
        var totalTags = await _tagRepo.GetTotalCountAsync();
        var totalFaces = await _faceRepo.GetTotalCountAsync();
        var personSummary = await _faceRepo.GetPersonSummaryAsync();

        var totalFileSizeBytes = await _db.Photos.SumAsync(p => (long?)p.FileSize) ?? 0;
        var photosIndexedToday = await _db.Photos
            .CountAsync(p => p.LastIndexedAt != null && p.LastIndexedAt.Value.Date == DateTime.UtcNow.Date);

        var today = DateTime.UtcNow.Date;
        var photosToday = await _db.Photos
            .CountAsync(p => p.CreatedAt >= today);

        return Ok(new DashboardStatsDto
        {
            TotalPhotos = totalPhotos,
            TotalAlbums = totalAlbums,
            TotalTags = totalTags,
            TotalFaces = totalFaces,
            TotalFileSizeBytes = totalFileSizeBytes,
            PhotosIndexedToday = photosToday,
            TotalPersons = personSummary.Count(p => !string.IsNullOrEmpty(p.PersonName))
        });
    }

    [HttpGet("photos-by-month")]
    public async Task<ActionResult<List<MonthlyStatsDto>>> GetPhotosByMonth()
    {
        var photos = await _db.Photos
            .Select(p => new { p.DateTaken, p.CreatedAt })
            .ToListAsync();

        var byMonth = photos
            .Select(p => p.DateTaken ?? p.CreatedAt)
            .Where(d => d != default)
            .GroupBy(d => new { d.Year, d.Month })
            .Select(g => new MonthlyStatsDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Count = g.Count()
            })
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .ToList();

        return Ok(byMonth);
    }

    [HttpGet("top-tags")]
    public async Task<ActionResult<List<TopTagDto>>> GetTopTags(
        [FromQuery] int limit = 20)
    {
        var tags = await _tagRepo.GetTopTagsAsync(limit);
        return Ok(tags.Select(t => new TopTagDto
        {
            Name = t.Tag.Name,
            Category = t.Tag.Category,
            UsageCount = t.Count
        }).ToList());
    }
}
