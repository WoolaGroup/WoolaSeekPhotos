using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Domain.Entities;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.Infrastructure.Repositories;

public class PhotoRepository : IPhotoRepository
{
    private readonly WoolaDbContext _db;

    public PhotoRepository(WoolaDbContext db) => _db = db;

    public async Task<Photo?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Photos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Photo?> GetByHashAsync(string hash, CancellationToken ct = default)
        => await _db.Photos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Hash == hash, ct);

    public async Task<Photo?> GetByPathAsync(string path, CancellationToken ct = default)
        => await _db.Photos
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Path == path, ct);

    public async Task<PagedResult<Photo>> GetPhotosAsync(
        int page, int pageSize, string? sortBy, string? sortDir,
        CancellationToken ct = default)
    {
        var query = _db.Photos.AsNoTracking();

        query = (sortBy, sortDir) switch
        {
            ("dateTaken", "asc") => query.OrderBy(p => p.DateTaken ?? p.CreatedAt),
            ("dateTaken", _) => query.OrderByDescending(p => p.DateTaken ?? p.CreatedAt),
            ("name", "asc") => query.OrderBy(p => p.Path),
            ("name", _) => query.OrderByDescending(p => p.Path),
            ("size", "asc") => query.OrderBy(p => p.FileSize),
            ("size", _) => query.OrderByDescending(p => p.FileSize),
            ("camera", "asc") => query.OrderBy(p => p.CameraModel ?? ""),
            ("camera", _) => query.OrderByDescending(p => p.CameraModel ?? ""),
            _ => query.OrderByDescending(p => p.DateTaken ?? p.CreatedAt)
        };

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Photo>(items, total, page, pageSize);
    }

    public async Task<PagedResult<Photo>> SearchAsync(
        string query, int page, int pageSize,
        CancellationToken ct = default)
    {
        var search = query.ToLower();
        var q = _db.Photos.AsNoTracking()
            .Where(p => p.Path.ToLower().Contains(search)
                     || p.CameraModel!.ToLower().Contains(search)
                     || p.PhotoTags.Any(pt => pt.Tag!.Name.ToLower().Contains(search)));

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(p => p.DateTaken ?? p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Photo>(items, total, page, pageSize);
    }

    public async Task<int> InsertAsync(Photo entity, CancellationToken ct = default)
    {
        _db.Photos.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<int> InsertBatchAsync(IEnumerable<Photo> photos, CancellationToken ct = default)
    {
        _db.Photos.AddRange(photos);
        return await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Photo entity, CancellationToken ct = default)
    {
        _db.Photos.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var photo = await _db.Photos.FindAsync(new object[] { id }, ct);
        if (photo != null)
        {
            photo.SoftDelete();
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await _db.Photos.CountAsync(ct);

    public async Task UpdateStatusAsync(int id, string status, CancellationToken ct = default)
    {
        await _db.Photos
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, status)
                .SetProperty(p => p.LastIndexedAt, DateTime.UtcNow),
                ct);
    }

    public async Task UpdateThumbnailAsync(int id, string thumbnailPath, CancellationToken ct = default)
    {
        await _db.Photos
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.ThumbnailPath, thumbnailPath),
                ct);
    }

    public async Task<List<Photo>> GetByDateRangeAsync(DateTime start, DateTime end, CancellationToken ct = default)
        => await _db.Photos.AsNoTracking()
            .Where(p => p.DateTaken >= start && p.DateTaken <= end)
            .OrderBy(p => p.DateTaken)
            .ToListAsync(ct);

    public async Task<List<Photo>> GetWithGpsAsync(CancellationToken ct = default)
        => await _db.Photos.AsNoTracking()
            .Where(p => p.Latitude != null && p.Longitude != null)
            .ToListAsync(ct);
}
