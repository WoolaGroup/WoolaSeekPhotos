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
            INSERT INTO Photos (Path, Hash, FileSize, DateTaken, Width, Height, Latitude, Longitude, CameraModel, LensModel, Aperture, ShutterSpeed, Iso, FocalLength, Orientation, Status, ThumbnailPath, CreatedAt, LastIndexedAt)
            VALUES (@Path, @Hash, @FileSize, @DateTaken, @Width, @Height, @Latitude, @Longitude, @CameraModel, @LensModel, @Aperture, @ShutterSpeed, @Iso, @FocalLength, @Orientation, @Status, @ThumbnailPath, @CreatedAt, @LastIndexedAt);
            SELECT last_insert_rowid();
        ";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            photo.Path,
            photo.Hash,
            photo.FileSize,
            DateTaken = photo.DateTaken?.ToIsoString(),
            photo.Width,
            photo.Height,
            photo.Latitude,
            photo.Longitude,
            photo.CameraModel,
            photo.LensModel,
            photo.Aperture,
            photo.ShutterSpeed,
            photo.Iso,
            photo.FocalLength,
            photo.Orientation,
            photo.Status,
            photo.ThumbnailPath,
            CreatedAt = photo.CreatedAt.ToIsoString(),
            LastIndexedAt = photo.LastIndexedAt?.ToIsoString()
        });
    }

    public async Task<IEnumerable<Photo>> GetPhotosAsync(int limit = 1000, int offset = 0)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, Path, Hash, FileSize, DateTaken, Width, Height,
                   Latitude, Longitude, CameraModel, LensModel, Aperture, ShutterSpeed, Iso, FocalLength, Orientation,
                   Status, ThumbnailPath, CreatedAt, LastIndexedAt
            FROM Photos
            ORDER BY COALESCE(DateTaken, CreatedAt) DESC
            LIMIT @Limit OFFSET @Offset
        ";

        var photos = await connection.QueryAsync<Photo>(sql, new { Limit = limit, Offset = offset });

        foreach (var photo in photos)
        {
            if (photo.DateTaken.HasValue)
            {
                // Conversión si es necesario
            }
        }

        return photos;
    }

    public async Task<Photo?> GetPhotoByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, Path, Hash, FileSize, DateTaken, Width, Height,
                   Latitude, Longitude, CameraModel, LensModel, Aperture, ShutterSpeed, Iso, FocalLength, Orientation,
                   Status, ThumbnailPath, CreatedAt, LastIndexedAt
            FROM Photos
            WHERE Id = @Id
        ";

        return await connection.QueryFirstOrDefaultAsync<Photo>(sql, new { Id = id });
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
            SELECT p.Id, p.Path, p.Hash, p.FileSize, p.DateTaken, p.Width, p.Height,
                   p.Latitude, p.Longitude, p.CameraModel, p.LensModel, p.Aperture, p.ShutterSpeed, p.Iso, p.FocalLength, p.Orientation,
                   p.Status, p.ThumbnailPath
            FROM Photos_FTS fts
            JOIN Photos p ON p.Id = fts.rowid
            WHERE Photos_FTS MATCH @SearchTerm
            ORDER BY COALESCE(p.DateTaken, p.CreatedAt) DESC
            LIMIT @Limit
        ";

        return await connection.QueryAsync<Photo>(sql, new { SearchTerm = searchTerm, Limit = limit });
    }

    // IMP-T3-003: CancellationToken para poder cancelar queries supersedidas
    public async Task<List<Photo>> SearchCandidatesAsync(
        string query, int limit = 500, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var likeQuery = $"%{query}%";
        const string sql = @"
            SELECT Id, Path, Hash, FileSize, DateTaken, Width, Height,
                   Latitude, Longitude, CameraModel, LensModel, Aperture, ShutterSpeed,
                   Iso, FocalLength, Orientation, Status, ThumbnailPath, CreatedAt, LastIndexedAt
            FROM Photos
            WHERE Id IN (
                SELECT pt.PhotoId FROM PhotoTags pt
                JOIN Tags t ON t.Id = pt.TagId
                WHERE t.Name LIKE @Query
                UNION
                SELECT Id FROM Photos WHERE Path LIKE @Query OR CameraModel LIKE @Query
            )
            ORDER BY COALESCE(DateTaken, CreatedAt) DESC
            LIMIT @Limit";
        return (await connection.QueryAsync<Photo>(
            new CommandDefinition(sql, new { Query = likeQuery, Limit = limit },
                cancellationToken: cancellationToken))).ToList();
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

    // ── G3: Event detection helpers ──────────────────────────────────────────

    /// <summary>
    /// Todos los DateTaken (un valor por foto, con duplicados) para clustering en memoria.
    /// </summary>
    public async Task<IEnumerable<DateTime>> GetAllDateTakenAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<DateTime>(
            "SELECT DateTaken FROM Photos WHERE DateTaken IS NOT NULL ORDER BY DateTaken");
    }

    /// <summary>
    /// Fotos cuyo DateTaken cae en [start, end) — end es exclusivo.
    /// </summary>
    public async Task<IEnumerable<Photo>> GetPhotosByDateRangeAsync(
        DateTime start, DateTime end, int limit = 2000)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, Path, Hash, FileSize, DateTaken, Width, Height,
                   Latitude, Longitude, CameraModel, LensModel, Aperture, ShutterSpeed,
                   Iso, FocalLength, Orientation, Status, ThumbnailPath, CreatedAt, LastIndexedAt
            FROM Photos
            WHERE DateTaken >= @Start AND DateTaken < @End
            ORDER BY DateTaken DESC
            LIMIT @Limit";
        return await connection.QueryAsync<Photo>(sql, new
        {
            Start = start.ToIsoString(),
            End   = end.ToIsoString(),
            Limit = limit
        });
    }
}