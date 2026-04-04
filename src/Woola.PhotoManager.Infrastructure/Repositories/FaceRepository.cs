using Dapper;
using Microsoft.Data.Sqlite;
using Woola.PhotoManager.Common.Helpers;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Database;

namespace Woola.PhotoManager.Infrastructure.Repositories;

public class FaceRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public FaceRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> InsertFaceAsync(Face face)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            INSERT INTO Faces (PhotoId, PersonName, PersonId, X, Y, Width, Height, Encoding, Confidence, IsUserConfirmed, CreatedAt)
            VALUES (@PhotoId, @PersonName, @PersonId, @X, @Y, @Width, @Height, @Encoding, @Confidence, @IsUserConfirmed, @CreatedAt);
            SELECT last_insert_rowid();
        ";

        return await connection.ExecuteScalarAsync<int>(sql, new
        {
            face.PhotoId,
            face.PersonName,
            face.PersonId,
            face.X,
            face.Y,
            face.Width,
            face.Height,
            face.Encoding,
            face.Confidence,
            face.IsUserConfirmed,
            CreatedAt = DateTime.UtcNow.ToIsoString()
        });
    }

    public async Task<IEnumerable<Face>> GetAllFacesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            SELECT Id, PhotoId, PersonName, PersonId, X, Y, Width, Height, Confidence, IsUserConfirmed, CreatedAt
            FROM Faces
            ORDER BY CreatedAt DESC
        ";

        return await connection.QueryAsync<Face>(sql);
    }

    public async Task UpdatePersonNameAsync(int faceId, string personName, string personId)
    {
        using var connection = _connectionFactory.CreateConnection();

        const string sql = @"
            UPDATE Faces 
            SET PersonName = @PersonName, PersonId = @PersonId, IsUserConfirmed = 1
            WHERE Id = @Id
        ";

        await connection.ExecuteAsync(sql, new { Id = faceId, PersonName = personName, PersonId = personId });
    }

    public async Task<IEnumerable<Face>> GetFacesForPhotoAsync(int photoId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, PhotoId, PersonName, PersonId, X, Y, Width, Height,
                   Confidence, IsUserConfirmed, CreatedAt
            FROM Faces
            WHERE PhotoId = @PhotoId
            ORDER BY Confidence DESC
        ";
        return await connection.QueryAsync<Face>(sql, new { PhotoId = photoId });
    }

    public async Task DeleteAllFacesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "DELETE FROM Faces";
        await connection.ExecuteAsync(sql);
    }

    public async Task UpdatePersonIdAsync(int faceId, string personId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "UPDATE Faces SET PersonId = @PersonId WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = faceId, PersonId = personId });
    }

    public async Task<IEnumerable<Face>> GetFacesWithEncodingAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Id, PhotoId, PersonName, PersonId, X, Y, Width, Height,
                   Encoding, Confidence, IsUserConfirmed, CreatedAt
            FROM Faces WHERE Encoding IS NOT NULL
            ORDER BY CreatedAt DESC";
        return await connection.QueryAsync<Face>(sql);
    }
}