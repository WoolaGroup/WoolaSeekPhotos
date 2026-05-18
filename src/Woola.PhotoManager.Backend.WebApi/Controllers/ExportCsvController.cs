using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ExportCsvController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public ExportCsvController(WoolaDbContext db) => _db = db;

    [HttpPost("photos")]
    public async Task<ActionResult> ExportPhotos([FromBody] ExportCsvRequest request)
    {
        var photos = await _db.Photos
            .AsNoTracking()
            .Where(p => request.PhotoIds.Contains(p.Id))
            .OrderByDescending(p => p.DateTaken ?? p.CreatedAt)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("ID,FileName,DateTaken,CameraModel,LensModel,Aperture,ISO,FocalLength,FileSize,Width,Height,Latitude,Longitude,Status,CreatedAt");

        foreach (var p in photos)
        {
            sb.AppendLine(string.Join(",",
                p.Id,
                CsvEscape(p.FileName),
                p.DateTaken?.ToString("yyyy-MM-dd HH:mm:ss"),
                CsvEscape(p.CameraModel),
                CsvEscape(p.LensModel),
                p.Aperture?.ToString("F1"),
                p.Iso?.ToString(),
                p.FocalLength?.ToString(),
                p.FileSize.ToString(),
                p.Width.ToString(), p.Height.ToString(),
                p.Latitude?.ToString("F6", CultureInfo.InvariantCulture),
                p.Longitude?.ToString("F6", CultureInfo.InvariantCulture),
                CsvEscape(p.Status),
                p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv",
            $"woola-export-{request.PhotoIds.Count}photos-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string CsvEscape(string? value) =>
        string.IsNullOrEmpty(value) ? "" : $"\"{value.Replace("\"", "\"\"")}\"";
}

public class ExportCsvRequest
{
    public List<int> PhotoIds { get; set; } = new();
}
