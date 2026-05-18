using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BackupController : ControllerBase
{
    private readonly ILogger<BackupController> _logger;

    public BackupController(ILogger<BackupController> logger) => _logger = logger;

    [HttpGet("export")]
    public async Task<ActionResult> Export()
    {
        var woolaDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WoolaPhotos");

        var dbPath = Path.Combine(woolaDir, "photos.db");
        var thumbDir = Path.Combine(woolaDir, "thumbnails");

        if (!System.IO.File.Exists(dbPath))
            return NotFound("Database not found");

        var tempZip = Path.Combine(Path.GetTempPath(), $"woola-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");

        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create);

                archive.CreateEntryFromFile(dbPath, "photos.db");

                if (Directory.Exists(thumbDir))
                {
                    foreach (var file in Directory.GetFiles(thumbDir, "*.jpg"))
                    {
                        archive.CreateEntryFromFile(file, $"thumbnails/{Path.GetFileName(file)}");
                    }
                }

                var settingsPath = Path.Combine(woolaDir, "settings.json");
                if (System.IO.File.Exists(settingsPath))
                    archive.CreateEntryFromFile(settingsPath, "settings.json");
            });

            var bytes = await System.IO.File.ReadAllBytesAsync(tempZip);
            System.IO.File.Delete(tempZip);

            return File(bytes, "application/zip", $"woola-backup-{DateTime.UtcNow:yyyyMMdd}.zip");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("info")]
    public ActionResult GetInfo()
    {
        var woolaDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WoolaPhotos");

        var dbPath = Path.Combine(woolaDir, "photos.db");
        var thumbDir = Path.Combine(woolaDir, "thumbnails");

        var dbExists = System.IO.File.Exists(dbPath);
        var thumbCount = Directory.Exists(thumbDir) ? Directory.GetFiles(thumbDir, "*.jpg").Length : 0;
        var dbSize = dbExists ? new FileInfo(dbPath).Length : 0;

        return Ok(new
        {
            databasePath = dbPath,
            databaseExists = dbExists,
            databaseSize = $"{dbSize / 1024.0:F0} KB",
            thumbnailCount = thumbCount,
            backupAvailable = dbExists
        });
    }
}
