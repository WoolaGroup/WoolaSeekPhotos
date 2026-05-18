using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Woola.PhotoManager.Backend.Infrastructure.Data;

public class WoolaDbContextFactory : IDesignTimeDbContextFactory<WoolaDbContext>
{
    public WoolaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WoolaDbContext>();
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WoolaPhotos", "photos.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        return new WoolaDbContext(optionsBuilder.Options);
    }
}
