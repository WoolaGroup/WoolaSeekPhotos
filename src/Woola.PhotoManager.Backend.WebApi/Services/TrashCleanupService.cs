using Woola.PhotoManager.Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Woola.PhotoManager.Backend.WebApi.Services;

public class TrashCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TrashCleanupService> _logger;

    public TrashCleanupService(IServiceProvider services, ILogger<TrashCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WoolaDbContext>();
                var cutoff = DateTime.UtcNow.AddDays(-30);

                var oldDeleted = await db.Photos.IgnoreQueryFilters()
                    .Where(p => p.IsDeleted && p.UpdatedAt != null && p.UpdatedAt.Value < cutoff)
                    .ToListAsync(stoppingToken);

                foreach (var photo in oldDeleted)
                {
                    if (!string.IsNullOrEmpty(photo.Path) && System.IO.File.Exists(photo.Path))
                        System.IO.File.Delete(photo.Path);
                    var thumbPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "WoolaPhotos", "thumbnails", photo.ThumbnailPath ?? "");
                    if (System.IO.File.Exists(thumbPath))
                        System.IO.File.Delete(thumbPath);
                    db.Photos.Remove(photo);
                }

                await db.SaveChangesAsync(stoppingToken);
                if (oldDeleted.Count > 0)
                    _logger.LogInformation("Auto-cleaned {Count} trash items older than 30 days", oldDeleted.Count);
            }
            catch (Exception ex) { _logger.LogError(ex, "Trash cleanup error"); }

            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
