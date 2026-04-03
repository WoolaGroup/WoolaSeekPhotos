using Dapper;
using Microsoft.Data.Sqlite;
using Woola.PhotoManager.Common.Helpers;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Database;

namespace Woola.PhotoManager.Infrastructure.Repositories;

public class PhotoRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public PhotoRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> InsertPhotoAsync(Photo photo)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
        INSERT INTO Photos (Path, Hash, FileSize, DateTaken, Width, Height, Status, ThumbnailPath, CreatedAt, LastIndexedAt)
        VALUES (@Path, @Hash, @FileSize, @DateTaken, @Width, @Height, @Status, @ThumbnailPath, @CreatedAt, @LastIndexedAt);
        SELECT last_insert_rowid();
    ";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            photo.Path,
            photo.Hash,
            photo.FileSize,
            DateTaken = photo.DateTaken?.ToIsoString() ?? null,  // ← Si es null, guarda null
            photo.Width,
            photo.Height,
            photo.Status,
            photo.ThumbnailPath,
            CreatedAt = photo.CreatedAt.ToIsoString(),  // ← Siempre tiene valor
            LastIndexedAt = photo.LastIndexedAt?.ToIsoString() ?? null
        });
    }
    public async Task<IEnumerable<Photo>> GetPhotosAsync(int limit = 100, int offset = 0)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
        SELECT Id, Path, Hash, FileSize, DateTaken, Width, Height, Status, ThumbnailPath, CreatedAt, LastIndexedAt
        FROM Photos
        ORDER BY Id
        LIMIT @Limit OFFSET @Offset
    ";

        var photos = await connection.QueryAsync<Photo>(sql, new { Limit = limit, Offset = offset });

        foreach (var photo in photos)
        {
            // Convertir DateTaken
            if (!string.IsNullOrEmpty(photo.DateTakenString))
            {
                try
                {
                    photo.DateTaken = DateTimeHelper.FromIsoString(photo.DateTakenString);
                }
                catch
                {
                    photo.DateTaken = null;
                }
            }

            // Convertir CreatedAt (siempre debería tener valor)
            if (!string.IsNullOrEmpty(photo.CreatedAtString))
            {
                try
                {
                    photo.CreatedAt = DateTimeHelper.FromIsoString(photo.CreatedAtString);
                }
                catch
                {
                    photo.CreatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                photo.CreatedAt = DateTime.UtcNow;
            }

            // Convertir LastIndexedAt
            if (!string.IsNullOrEmpty(photo.LastIndexedAtString))
            {
                try
                {
                    photo.LastIndexedAt = DateTimeHelper.FromIsoString(photo.LastIndexedAtString);
                }
                catch
                {
                    photo.LastIndexedAt = null;
                }
            }
        }

        return photos;
    }
    public async Task<int> GetTotalCountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Photos");
    }

    public async Task<IEnumerable<Photo>> SearchPhotosAsync(string searchTerm, int limit = 100)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT p.Id, p.Path, p.Hash, p.FileSize, p.DateTaken, p.Width, p.Height, p.Status, p.ThumbnailPath
            FROM Photos_FTS fts
            JOIN Photos p ON p.Id = fts.rowid
            WHERE Photos_FTS MATCH @SearchTerm
            LIMIT @Limit
        ";

        var photos = await connection.QueryAsync<Photo>(sql, new { SearchTerm = searchTerm, Limit = limit });

        foreach (var photo in photos)
        {
            if (!string.IsNullOrEmpty(photo.DateTakenString))
            {
                photo.DateTaken = DateTimeHelper.FromIsoString(photo.DateTakenString);
            }
        }

        return photos;
    }

    public async Task UpdatePhotoStatusAsync(int id, string status)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE Photos SET Status = @Status WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id, Status = status });
    }

    public async Task<bool> PhotoExistsAsync(string hash)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM Photos WHERE Hash = @Hash)",
            new { Hash = hash });
    }

    public async Task UpdateThumbnailPathAsync(int id, string thumbnailPath)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE Photos SET ThumbnailPath = @ThumbnailPath WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id, ThumbnailPath = thumbnailPath });
    }
}