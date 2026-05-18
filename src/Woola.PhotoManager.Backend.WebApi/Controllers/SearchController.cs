using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Infrastructure.Data;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SearchController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public SearchController(WoolaDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<PhotoDto>>> AdvancedSearch(
        [FromQuery] string? q,
        [FromQuery] string? camera,
        [FromQuery] string? lens,
        [FromQuery] int? isoMin, [FromQuery] int? isoMax,
        [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo,
        [FromQuery] string? tags,
        [FromQuery] int? ratingMin,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var query = _db.Photos.AsNoTracking();

        if (!string.IsNullOrEmpty(q))
        {
            var term = q.ToLower();
            query = query.Where(p => p.FileName.ToLower().Contains(term)
                                  || p.CameraModel!.ToLower().Contains(term)
                                  || p.LensModel!.ToLower().Contains(term));
        }

        if (!string.IsNullOrEmpty(camera))
            query = query.Where(p => p.CameraModel != null && p.CameraModel.Contains(camera));

        if (!string.IsNullOrEmpty(lens))
            query = query.Where(p => p.LensModel != null && p.LensModel.Contains(lens));

        if (isoMin.HasValue)
            query = query.Where(p => p.Iso >= isoMin);
        if (isoMax.HasValue)
            query = query.Where(p => p.Iso <= isoMax);

        if (dateFrom.HasValue)
            query = query.Where(p => p.DateTaken >= dateFrom);
        if (dateTo.HasValue)
            query = query.Where(p => p.DateTaken <= dateTo);

        if (!string.IsNullOrEmpty(tags))
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            query = query.Where(p => p.PhotoTags.Any(pt => tagList.Contains(pt.Tag!.Name)));
        }

        query = query.OrderByDescending(p => p.DateTaken ?? p.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(PagedApiResponse<PhotoDto>.Ok(
            items.Select(p => new PhotoDto
            {
                Id = p.Id, FileName = p.FileName, Path = p.Path,
                FileSize = p.FileSize, DateTaken = p.DateTaken,
                Width = p.Width, Height = p.Height,
                CameraModel = p.CameraModel, LensModel = p.LensModel,
                Iso = p.Iso, Aperture = p.Aperture, FocalLength = p.FocalLength,
                ThumbnailUrl = p.ThumbnailPath != null ? $"/thumbnails/{p.ThumbnailPath}" : null,
                CreatedAt = p.CreatedAt
            }).ToList(), total, page, pageSize));
    }
}
