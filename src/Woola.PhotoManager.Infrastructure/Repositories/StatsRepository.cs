using Dapper;
using Woola.PhotoManager.Infrastructure.Database;

namespace Woola.PhotoManager.Infrastructure.Repositories;

public record StatsOverview(int Photos, int Tags, int Faces, int Albums);
public record TagStat(string Name, long Count);
public record MonthStat(string Month, long Count);
public record SourceStat(string Source, long Count);

public class StatsRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public StatsRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<StatsOverview> GetOverviewAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var photos  = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Photos");
        var tags    = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Tags");
        var faces   = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Faces");
        var albums  = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Albums");
        return new StatsOverview(photos, tags, faces, albums);
    }

    public async Task<List<TagStat>> GetTopTagsAsync(int limit = 15)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT t.Name, COUNT(pt.PhotoId) AS Count
            FROM Tags t
            JOIN PhotoTags pt ON pt.TagId = t.Id
            GROUP BY t.Id
            ORDER BY Count DESC
            LIMIT @Limit";
        return (await connection.QueryAsync<TagStat>(sql, new { Limit = limit })).ToList();
    }

    /// <summary>Fotos indexadas por mes (últimos N meses), en orden cronológico.</summary>
    public async Task<List<MonthStat>> GetPhotosByMonthAsync(int months = 12)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT strftime('%Y-%m', COALESCE(DateTaken, CreatedAt)) AS Month,
                   COUNT(*) AS Count
            FROM Photos
            GROUP BY Month
            ORDER BY Month DESC
            LIMIT @Months";
        var rows = (await connection.QueryAsync<MonthStat>(sql, new { Months = months })).ToList();
        rows.Reverse(); // orden cronológico
        return rows;
    }

    /// <summary>Cantidad de tags generados por cada agente.</summary>
    public async Task<List<SourceStat>> GetTagsBySourceAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = @"
            SELECT Source, COUNT(*) AS Count
            FROM PhotoTags
            GROUP BY Source
            ORDER BY Count DESC";
        return (await connection.QueryAsync<SourceStat>(sql)).ToList();
    }

    public async Task<int> GetPhotosIndexedTodayAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        // Comparación de prefijo funciona porque CreatedAt es ISO 8601
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Photos WHERE CreatedAt >= @Today",
            new { Today = today });
    }
}
