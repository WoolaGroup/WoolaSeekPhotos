using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Domain.Entities;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.Infrastructure.Repositories;

public class FaceRepository : IFaceRepository
{
    private readonly WoolaDbContext _db;

    public FaceRepository(WoolaDbContext db) => _db = db;

    public async Task<Face?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _db.Faces.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<List<Face>> GetAllAsync(CancellationToken ct = default)
        => await _db.Faces.AsNoTracking().ToListAsync(ct);

    public async Task<List<Face>> GetForPhotoAsync(int photoId, CancellationToken ct = default)
        => await _db.Faces
            .Where(f => f.PhotoId == photoId)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<List<Face>> GetWithEncodingsAsync(CancellationToken ct = default)
        => await _db.Faces
            .Where(f => f.Encoding != null)
            .AsNoTracking()
            .ToListAsync(ct);

    public async Task<int> InsertAsync(Face entity, CancellationToken ct = default)
    {
        _db.Faces.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(Face entity, CancellationToken ct = default)
    {
        _db.Faces.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var face = await _db.Faces.FindAsync(new object[] { id }, ct);
        if (face != null)
        {
            face.SoftDelete();
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await _db.Faces.CountAsync(ct);

    public async Task UpdatePersonAsync(int faceId, string? personName, string? personId, CancellationToken ct = default)
    {
        var face = await _db.Faces.FindAsync(new object[] { faceId }, ct);
        if (face != null)
        {
            face.SetPerson(personName, personId);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<(string? PersonName, int Count)>> GetPersonSummaryAsync(CancellationToken ct = default)
    {
        var results = await _db.Faces
            .GroupBy(f => f.PersonName)
            .Select(g => new { PersonName = g.Key, Count = g.Count() })
            .AsNoTracking()
            .ToListAsync(ct);

        return results.Select(r => (r.PersonName, r.Count)).ToList();
    }

    public async Task<PagedResult<Photo>> GetPhotosByPersonAsync(
        string personName, int page, int pageSize, CancellationToken ct = default)
    {
        var photoIds = await _db.Faces
            .Where(f => f.PersonName == personName)
            .Select(f => f.PhotoId)
            .Distinct()
            .ToListAsync(ct);

        var total = photoIds.Count;
        var photos = await _db.Photos.AsNoTracking()
            .Where(p => photoIds.Contains(p.Id))
            .OrderByDescending(p => p.DateTaken ?? p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Photo>(photos, total, page, pageSize);
    }

    public async Task DeleteForPhotoAsync(int photoId, CancellationToken ct = default)
    {
        await _db.Faces.Where(f => f.PhotoId == photoId)
            .ExecuteDeleteAsync(ct);
    }
}
