using Dapper;
using Woola.PhotoManager.Common.Helpers;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Database;

namespace Woola.PhotoManager.Infrastructure.Repositories;

public class AlbumRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public AlbumRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <summary>Devuelve todos los álbumes con su PhotoCount y la miniatura de portada.</summary>
    public async Task<List<Album>> GetAllAlbumsAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT a.Id, a.Name, a.Description, a.CoverPhotoId, a.CreatedAt,
                   COUNT(ap.PhotoId) AS PhotoCount,
                   COALESCE(
                       (SELECT ThumbnailPath FROM Photos WHERE Id = a.CoverPhotoId),
                       (SELECT p2.ThumbnailPath FROM AlbumPhotos ap2
                        JOIN Photos p2 ON p2.Id = ap2.PhotoId
                        WHERE ap2.AlbumId = a.Id ORDER BY ap2.AddedAt LIMIT 1)
                   ) AS CoverThumbnailPath
            FROM Albums a
            LEFT JOIN AlbumPhotos ap ON ap.AlbumId = a.Id
            GROUP BY a.Id
            ORDER BY a.CreatedAt DESC";
        return (await connection.QueryAsync<Album>(sql)).ToList();
    }

    public async Task<int> CreateAlbumAsync(string name, string? description = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            INSERT INTO Albums (Name, Description, CreatedAt)
            VALUES (@Name, @Description, @CreatedAt);
            SELECT last_insert_rowid();";
        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow.ToIsoString()
        });
    }

    public async Task DeleteAlbumAsync(int albumId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM Albums WHERE Id = @Id", new { Id = albumId });
    }

    public async Task AddPhotoToAlbumAsync(int albumId, int photoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(@"
            INSERT OR IGNORE INTO AlbumPhotos (AlbumId, PhotoId, AddedAt)
            VALUES (@AlbumId, @PhotoId, @AddedAt)",
            new { AlbumId = albumId, PhotoId = photoId, AddedAt = DateTime.UtcNow.ToIsoString() });
    }

    public async Task RemovePhotoFromAlbumAsync(int albumId, int photoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM AlbumPhotos WHERE AlbumId = @AlbumId AND PhotoId = @PhotoId",
            new { AlbumId = albumId, PhotoId = photoId });
    }

    public async Task<IEnumerable<Photo>> GetPhotosInAlbumAsync(int albumId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT p.Id, p.Path, p.Hash, p.FileSize, p.DateTaken, p.Width, p.Height,
                   p.Latitude, p.Longitude, p.CameraModel, p.LensModel, p.Aperture, p.ShutterSpeed,
                   p.Iso, p.FocalLength, p.Orientation, p.Status, p.ThumbnailPath,
                   p.CreatedAt, p.LastIndexedAt
            FROM AlbumPhotos ap
            JOIN Photos p ON p.Id = ap.PhotoId
            WHERE ap.AlbumId = @AlbumId
            ORDER BY ap.AddedAt DESC";
        return await connection.QueryAsync<Photo>(sql, new { AlbumId = albumId });
    }

    /// <summary>Devuelve los IDs de fotos que pertenecen al álbum (útil para el AlbumWindow).</summary>
    public async Task<HashSet<int>> GetPhotoIdsInAlbumAsync(int albumId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var ids = await connection.QueryAsync<int>(
            "SELECT PhotoId FROM AlbumPhotos WHERE AlbumId = @AlbumId",
            new { AlbumId = albumId });
        return ids.ToHashSet();
    }
}
