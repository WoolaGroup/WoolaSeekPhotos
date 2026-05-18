using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Frontend.MAUI.Services;

public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password);
    Task LogoutAsync();
}

public class AuthService : IAuthService
{
    private readonly PhotoApiClient _api;

    public AuthService(PhotoApiClient api) => _api = api;

    public async Task<bool> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _api.PostAsync<LoginRequest, LoginResponse>(
                "/api/v1/auth/login",
                new LoginRequest { Username = username, Password = password });

            _api.SetTokens(response.AccessToken, response.RefreshToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task LogoutAsync()
    {
        _api.ClearTokens();
        return Task.CompletedTask;
    }
}
