using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Infrastructure.Data;
using Woola.PhotoManager.Backend.WebApi.Services;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AdminController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public AdminController(WoolaDbContext db) => _db = db;

    [HttpPost("vacuum")]
    public async Task<ActionResult> Vacuum()
    {
        var before = await GetDbSizeAsync();
        await _db.Database.ExecuteSqlRawAsync("VACUUM");
        var after = await GetDbSizeAsync();
        return Ok(new { status = "vacuumed", beforeSize = before, afterSize = after, freed = before - after });
    }

    [HttpPost("reindex")]
    public async Task<ActionResult> Reindex()
    {
        await _db.Database.ExecuteSqlRawAsync("REINDEX");
        return Ok(new { status = "reindexed" });
    }

    [HttpGet("db-stats")]
    public async Task<ActionResult> DbStats()
    {
        var woolaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoolaPhotos");
        var dbPath = Path.Combine(woolaDir, "photos.db");
        var thumbDir = Path.Combine(woolaDir, "thumbnails");

        var dbExists = System.IO.File.Exists(dbPath);
        var thumbCount = Directory.Exists(thumbDir) ? Directory.GetFiles(thumbDir, "*.jpg").Length : 0;
        var totalPhotos = await _db.Photos.CountAsync();
        var deletedPhotos = await _db.Photos.IgnoreQueryFilters().CountAsync(p => p.IsDeleted);
        var totalFileSize = await _db.Photos.SumAsync(p => (long?)p.FileSize) ?? 0;
        var trashSize = await _db.Photos.IgnoreQueryFilters().Where(p => p.IsDeleted).SumAsync(p => (long?)p.FileSize) ?? 0;

        return Ok(new
        {
            database = new { exists = dbExists, size = dbExists ? new FileInfo(dbPath).Length : 0 },
            thumbnails = new { count = thumbCount, size = Directory.Exists(thumbDir) ? Directory.GetFiles(thumbDir, "*.jpg").Sum(f => new FileInfo(f).Length) : 0 },
            photos = new { total = totalPhotos, deleted = deletedPhotos, totalFileSize, trashSize },
            storage = new { dbSizeDisplay = FormatSize(dbExists ? new FileInfo(dbPath).Length : 0), thumbSizeDisplay = FormatSize(Directory.Exists(thumbDir) ? Directory.GetFiles(thumbDir, "*.jpg").Sum(f => new FileInfo(f).Length) : 0), totalFileSizeDisplay = FormatSize(totalFileSize), trashSizeDisplay = FormatSize(trashSize) }
        });
    }

    [HttpGet("backup-settings")]
    public ActionResult GetBackupSettings([FromServices] BackupScheduleService backupService)
    {
        return Ok(backupService.GetOptions());
    }

    [HttpPost("backup-settings")]
    public ActionResult UpdateBackupSettings([FromBody] BackupOptions options, [FromServices] BackupScheduleService backupService)
    {
        backupService.UpdateOptions(options);
        return Ok(new { status = "updated" });
    }

    [HttpPost("backup-now")]
    public async Task<ActionResult> BackupNow([FromServices] BackupScheduleService backupService)
    {
        await backupService.PerformBackupAsync();
        return Ok(new { status = "backup_completed", lastBackup = backupService.LastBackup });
    }

    [HttpGet("backup-status")]
    public ActionResult BackupStatus([FromServices] BackupScheduleService backupService)
    {
        return Ok(new { isEnabled = backupService.IsEnabled, lastBackup = backupService.LastBackup, statusMessage = backupService.StatusMessage });
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1048576 => $"{bytes / 1024.0:F0} KB",
        < 1073741824 => $"{bytes / 1048576.0:F1} MB",
        _ => $"{bytes / 1073741824.0:F2} GB"
    };

    private async Task<long> GetDbSizeAsync()
    {
        var woolaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoolaPhotos");
        var dbPath = Path.Combine(woolaDir, "photos.db");
        return System.IO.File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0;
    }
}
