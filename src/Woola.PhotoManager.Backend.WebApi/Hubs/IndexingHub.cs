using Microsoft.AspNetCore.SignalR;

namespace Woola.PhotoManager.Backend.WebApi.Hubs;

public class IndexingHub : Hub
{
    public async Task JoinIndexingGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "indexing");
    }

    public async Task LeaveIndexingGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "indexing");
    }
}

public static class IndexingHubExtensions
{
    public static async Task SendProgress(
        this IHubContext<IndexingHub> hub,
        int totalFound, int processed, double percentage, string currentFile,
        CancellationToken ct = default)
    {
        await hub.Clients.Group("indexing").SendAsync("ProgressReceived",
            new { totalFound, processed, percentage, currentFile }, ct);
    }

    public static async Task SendComplete(
        this IHubContext<IndexingHub> hub,
        int totalPhotos, long elapsedMs,
        CancellationToken ct = default)
    {
        await hub.Clients.Group("indexing").SendAsync("IndexingComplete",
            new { totalPhotos, elapsedMs }, ct);
    }
}
