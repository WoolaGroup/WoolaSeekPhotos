using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Frontend.MAUI.Services;

public interface IPhotoService
{
    Task<PagedApiResponse<PhotoDto>> GetPhotosAsync(int page = 1, int pageSize = 50,
        int? albumId = null, string? tag = null, string? search = null,
        string? sortBy = "dateTaken", string? sortDir = "desc");

    Task<PhotoDetailDto?> GetPhotoAsync(int id);
    Task InitiateIndexingAsync(string folderPath);
}

public class PhotoService : IPhotoService
{
    private readonly PhotoApiClient _api;

    public PhotoService(PhotoApiClient api) => _api = api;

    public async Task<PagedApiResponse<PhotoDto>> GetPhotosAsync(int page = 1, int pageSize = 50,
        int? albumId = null, string? tag = null, string? search = null,
        string? sortBy = "dateTaken", string? sortDir = "desc")
    {
        var query = $"/api/v1/photos?page={page}&pageSize={pageSize}";
        if (albumId.HasValue) query += $"&albumId={albumId}";
        if (!string.IsNullOrEmpty(tag)) query += $"&tag={Uri.EscapeDataString(tag)}";
        if (!string.IsNullOrEmpty(search)) query += $"&search={Uri.EscapeDataString(search)}";
        if (!string.IsNullOrEmpty(sortBy)) query += $"&sortBy={sortBy}";
        if (!string.IsNullOrEmpty(sortDir)) query += $"&sortDir={sortDir}";

        return await _api.GetAsync<PagedApiResponse<PhotoDto>>(query);
    }

    public async Task<PhotoDetailDto?> GetPhotoAsync(int id)
        => await _api.GetAsync<PhotoDetailDto>($"/api/v1/photos/{id}");

    public async Task InitiateIndexingAsync(string folderPath)
        => await _api.PostAsync("/api/v1/photos/index", new { folderPath });
}

public interface IAlbumService
{
    Task<List<AlbumDto>> GetAllAsync();
    Task<AlbumDto?> GetByIdAsync(int id);
    Task<AlbumDto> CreateAsync(string name, string? description);
    Task<PagedApiResponse<PhotoDto>> GetPhotosAsync(int albumId, int page = 1, int pageSize = 50);
    Task AddPhotosAsync(int albumId, List<int> photoIds);
}

public class AlbumService : IAlbumService
{
    private readonly PhotoApiClient _api;

    public AlbumService(PhotoApiClient api) => _api = api;

    public async Task<List<AlbumDto>> GetAllAsync()
        => await _api.GetAsync<List<AlbumDto>>("/api/v1/albums");

    public async Task<AlbumDto?> GetByIdAsync(int id)
        => await _api.GetAsync<AlbumDto?>($"/api/v1/albums/{id}");

    public async Task<AlbumDto> CreateAsync(string name, string? description)
        => await _api.PostAsync<object, AlbumDto>("/api/v1/albums", new { name, description });

    public async Task<PagedApiResponse<PhotoDto>> GetPhotosAsync(int albumId, int page = 1, int pageSize = 50)
        => await _api.GetAsync<PagedApiResponse<PhotoDto>>($"/api/v1/albums/{albumId}/photos?page={page}&pageSize={pageSize}");

    public async Task AddPhotosAsync(int albumId, List<int> photoIds)
        => await _api.PostAsync($"/api/v1/albums/{albumId}/photos", new { photoIds });
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync();
}

public class DashboardService : IDashboardService
{
    private readonly PhotoApiClient _api;

    public DashboardService(PhotoApiClient api) => _api = api;

    public async Task<DashboardStatsDto> GetStatsAsync()
        => await _api.GetAsync<DashboardStatsDto>("/api/v1/dashboard/stats");
}
