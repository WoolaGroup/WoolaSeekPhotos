using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Domain.Entities;

namespace Woola.PhotoManager.Backend.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(WoolaDbContext db, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        // Add Rating column if it doesn't exist (schema migration)
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "ALTER TABLE Photos ADD COLUMN Rating INTEGER NOT NULL DEFAULT 0", ct);
        }
        catch { /* column already exists */ }

        if (!await db.Tags.AnyAsync(ct))
        {
            var defaultTags = new List<Tag>
            {
                Tag.Create("Favorite", "System", false),
                Tag.Create("Screenshot", "System", true),
                Tag.Create("Edited", "System", false),
            };
            db.Tags.AddRange(defaultTags);
            await db.SaveChangesAsync(ct);
        }
    }
}
