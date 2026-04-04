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
            INSERT INTO Photos (
                Path, Hash, FileSize, DateTaken, Width, Height,
                Latitude, Longitude, CameraModel, LensModel, Aperture, ShutterSpeed, Iso, FocalLength, Orientation,
                Status, ThumbnailPath, CreatedAt, LastIndexedAt
            ) VALUES (
                @Path, @Hash, @FileSize, @DateTaken, @Width, @Height,
                @Latitude, @Longitude, @CameraModel, @LensModel, @Aperture, @ShutterSpeed, @Iso, @FocalLength, @Orientation,
                @Status, @ThumbnailPath, @CreatedAt, @LastIndexedAt
            );
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

        return photos;
    }

    public async Task<int> GetTotalCountAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Photos");
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

        var photo = await connection.QueryFirstOrDefaultAsync<Photo>(sql, new { Id = id });

        if (photo?.DateTaken != null && photo.DateTaken.HasValue)
        {
            // Ya está en el formato correcto
        }

        return photo;
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