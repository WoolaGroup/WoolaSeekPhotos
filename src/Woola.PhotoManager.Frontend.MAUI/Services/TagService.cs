using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Frontend.MAUI.Services;

public interface ITagService
{
    Task<List<TagDto>> GetAllAsync();
}

public class TagService : ITagService
{
    private readonly PhotoApiClient _api;

    public TagService(PhotoApiClient api) => _api = api;

    public async Task<List<TagDto>> GetAllAsync()
        => await _api.GetAsync<List<TagDto>>("/api/v1/tags");
}

public interface ISettingsService
{
    Task<Dictionary<string, bool>> GetAgentStatesAsync();
    Task SetAgentStateAsync(string agentName, bool enabled);
}

public class SettingsService : ISettingsService
{
    private readonly PhotoApiClient _api;

    public SettingsService(PhotoApiClient api) => _api = api;

    public async Task<Dictionary<string, bool>> GetAgentStatesAsync()
        => await _api.GetAsync<Dictionary<string, bool>>("/api/v1/settings/agents");

    public async Task SetAgentStateAsync(string agentName, bool enabled)
        => await _api.PutAsync<object, object>($"/api/v1/settings/agents/{agentName}", new { enabled });
}
