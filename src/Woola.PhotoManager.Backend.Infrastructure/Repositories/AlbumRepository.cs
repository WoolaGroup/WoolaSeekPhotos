using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Domain.Entities;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.Infrastructure.Repositories;

public class AlbumRepository : IAlbumRepository
{
    private readonly WoolaDbContext _db;

    public AlbumRepository(WoolaDbContext db) => _db = db;

    public async Task<Album?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Albums
            .Include(a => a.AlbumPhotos)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<List<Album>> GetAllAsync(CancellationToken ct = default)
        => await _db.Albums
            .Include(a => a.AlbumPhotos)
            .OrderByDescending(a => a.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<PagedResult<Photo>> GetPhotosAsync(
        int albumId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.AlbumPhotos
            .Where(ap => ap.AlbumId == albumId)
            .Include(ap => ap.Photo)
            .OrderByDescending(ap => ap.AddedAt)
            .AsNoTracking();

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ap => ap.Photo!)
            .ToListAsync(ct);

        return new PagedResult<Photo>(items, total, page, pageSize);
    }

    public async Task<int> InsertAsync(Album entity, CancellationToken ct = default)
    {
        _db.Albums.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(Album entity, CancellationToken ct = default)
    {
        _db.Albums.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var album = await _db.Albums.FindAsync(new object[] { id }, ct);
        if (album != null)
        {
            album.SoftDelete();
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await _db.Albums.CountAsync(ct);

    public async Task AddPhotoAsync(int albumId, int photoId, CancellationToken ct = default)
    {
        _db.AlbumPhotos.Add(AlbumPhoto.Create(albumId, photoId));

        var album = await _db.Albums.FindAsync(new object[] { albumId }, ct);
        if (album != null && album.CoverPhotoId == null)
            album.SetCover(photoId);

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemovePhotoAsync(int albumId, int photoId, CancellationToken ct = default)
    {
        var ap = await _db.AlbumPhotos
            .FirstOrDefaultAsync(x => x.AlbumId == albumId && x.PhotoId == photoId, ct);
        if (ap != null)
        {
            _db.AlbumPhotos.Remove(ap);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> HasPhotoAsync(int albumId, int photoId, CancellationToken ct = default)
        => await _db.AlbumPhotos
            .AnyAsync(x => x.AlbumId == albumId && x.PhotoId == photoId, ct);

    public async Task<List<int>> GetPhotoIdsAsync(int albumId, CancellationToken ct = default)
        => await _db.AlbumPhotos
            .Where(x => x.AlbumId == albumId)
            .OrderBy(x => x.SortOrder)
            .Select(x => x.PhotoId)
            .ToListAsync(ct);
}
