using Microsoft.AspNetCore.SignalR.Client;

namespace Woola.PhotoManager.Frontend.MAUI.Services;

public interface IIndexingHubClient
{
    event Action<int, int, double, string>? ProgressReceived;
    event Action<int, long>? IndexingComplete;
    Task ConnectAsync();
    Task DisconnectAsync();
}

public class IndexingHubClient : IIndexingHubClient, IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<int, int, double, string>? ProgressReceived;
    public event Action<int, long>? IndexingComplete;

    public async Task ConnectAsync()
    {
        _connection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5150/hubs/indexing")
            .WithAutomaticReconnect()
            .Build();

        _connection.On("ProgressReceived", (int totalFound, int processed, double percentage, string currentFile) =>
        {
            ProgressReceived?.Invoke(totalFound, processed, percentage, currentFile);
        });

        _connection.On("IndexingComplete", (int totalPhotos, long elapsedMs) =>
        {
            IndexingComplete?.Invoke(totalPhotos, elapsedMs);
        });

        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinIndexingGroup");
    }

    public async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.InvokeAsync("LeaveIndexingGroup");
            await _connection.StopAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        await (_connection?.DisposeAsync() ?? ValueTask.CompletedTask);
    }
}
