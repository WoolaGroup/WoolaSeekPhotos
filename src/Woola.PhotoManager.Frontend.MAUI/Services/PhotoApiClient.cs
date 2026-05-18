using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Frontend.MAUI.Services;

public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("Not authenticated") { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException() : base("Access denied") { }
}

public class PhotoApiClient : IDisposable
{
    private readonly HttpClient _http;
    private string? _accessToken;
    private string? _refreshToken;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _disposed;

    public PhotoApiClient(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri(BackendConfig.BaseUrl);
    }

    public void SetTokens(string accessToken, string? refreshToken = null)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken ?? accessToken;
    }

    public void ClearTokens()
    {
        _accessToken = null;
        _refreshToken = null;
    }

    public async Task<T> GetAsync<T>(string endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        await AttachTokenAsync(request);
        var response = await _http.SendAsync(request);
        response = await HandleUnauthorizedAsync<object?>(response, endpoint, HttpMethod.Get, null);
        return await DeserializeAsync<T>(response);
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(string endpoint, TRequest data)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(data) };
        await AttachTokenAsync(request);
        var response = await _http.SendAsync(request);
        response = await HandleUnauthorizedAsync<TRequest>(response, endpoint, HttpMethod.Post, data);
        return await DeserializeAsync<TResponse>(response);
    }

    public async Task PostAsync<TRequest>(string endpoint, TRequest data)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(data) };
        await AttachTokenAsync(request);
        var response = await _http.SendAsync(request);
        response = await HandleUnauthorizedAsync<TRequest>(response, endpoint, HttpMethod.Post, data);
        response.EnsureSuccessStatusCode();
    }

    public async Task<T> PutAsync<TRequest, T>(string endpoint, TRequest data) =>
        await SendAsync<TRequest, T>(HttpMethod.Put, endpoint, data);

    public async Task DeleteAsync(string endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        await AttachTokenAsync(request);
        var response = await _http.SendAsync(request);
        response = await HandleUnauthorizedAsync<object?>(response, endpoint, HttpMethod.Delete, null);
        response.EnsureSuccessStatusCode();
    }

    private async Task<T> SendAsync<TRequest, T>(HttpMethod method, string endpoint, TRequest? data = default)
    {
        var request = new HttpRequestMessage(method, endpoint);
        if (data != null) request.Content = JsonContent.Create(data);
        await AttachTokenAsync(request);
        var response = await _http.SendAsync(request);
        response = await HandleUnauthorizedAsync<TRequest>(response, endpoint, method, data);
        return await DeserializeAsync<T>(response);
    }

    private async Task AttachTokenAsync(HttpRequestMessage request)
    {
        if (_accessToken != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
    }

    private async Task<HttpResponseMessage> HandleUnauthorizedAsync<T>(
        HttpResponseMessage response, string endpoint, HttpMethod method, T? data)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized || _refreshToken == null)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new ForbiddenException();
            return response;
        }

        await _refreshLock.WaitAsync();
        try
        {
            var refreshResponse = await _http.PostAsJsonAsync("/api/v1/auth/refresh", new { accessToken = _accessToken, refreshToken = _refreshToken });
            if (refreshResponse.IsSuccessStatusCode)
            {
                var result = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
                if (result != null) { _accessToken = result.AccessToken; _refreshToken = result.RefreshToken; }
            }
            else { ClearTokens(); throw new UnauthorizedException(); }

            var retry = new HttpRequestMessage(method, endpoint);
            if (data != null) retry.Content = JsonContent.Create(data);
            await AttachTokenAsync(retry);
            return await _http.SendAsync(retry);
        }
        finally { _refreshLock.Release(); }
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<T>();
        return result ?? throw new InvalidOperationException("Empty response");
    }

    public void Dispose()
    {
        if (!_disposed) { _refreshLock.Dispose(); _disposed = true; }
    }
}
