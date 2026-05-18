using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ExportController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public ExportController(WoolaDbContext db) => _db = db;

    [HttpPost("photos")]
    public async Task<ActionResult> ExportPhotos([FromBody] ExportRequest request)
    {
        if (request.PhotoIds.Count == 0)
            return BadRequest("No photos selected");

        var photos = await _db.Photos
            .Where(p => request.PhotoIds.Contains(p.Id))
            .ToListAsync();

        var tempZip = Path.Combine(Path.GetTempPath(), $"woola-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");

        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create);

                foreach (var photo in photos)
                {
                    if (!System.IO.File.Exists(photo.Path)) continue;

                    var entryName = Path.GetFileName(photo.Path);
                    if (!string.IsNullOrEmpty(photo.DateTaken?.ToString("yyyy-MM-dd")))
                        entryName = $"{photo.DateTaken:yyyy-MM-dd}_{entryName}";

                    archive.CreateEntryFromFile(photo.Path, entryName);

                    if (photo.ThumbnailPath != null)
                    {
                        var thumbDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "WoolaPhotos", "thumbnails");
                        var thumbFile = Path.Combine(thumbDir, photo.ThumbnailPath);
                        if (System.IO.File.Exists(thumbFile))
                            archive.CreateEntryFromFile(thumbFile, $"thumbs/{photo.ThumbnailPath}");
                    }
                }

                // metadata.json
                var metadata = System.Text.Json.JsonSerializer.Serialize(
                    photos.Select(p => new
                    {
                        p.Id, p.FileName, p.DateTaken, p.CameraModel,
                        p.Latitude, p.Longitude, p.FileSize, p.Width, p.Height
                    }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                var entry = archive.CreateEntry("metadata.json");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(metadata);
            });

            var bytes = await System.IO.File.ReadAllBytesAsync(tempZip);
            System.IO.File.Delete(tempZip);

            return File(bytes, "application/zip",
                $"woola-export-{request.PhotoIds.Count}photos-{DateTime.UtcNow:yyyyMMdd}.zip");
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class ExportRequest
{
    public List<int> PhotoIds { get; set; } = new();
}
