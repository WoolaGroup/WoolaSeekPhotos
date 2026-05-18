using Woola.PhotoManager.Backend.Application.Common.Interfaces;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Infrastructure.Repositories;
using Woola.PhotoManager.Common.Services;

namespace Woola.PhotoManager.Backend.WebApi.Services;

public class WatchFolderService : BackgroundService
{
    private readonly ILogger<WatchFolderService> _logger;
    private FileSystemWatcher? _watcher;
    private string _folderPath = string.Empty;
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WoolaPhotos", "watchfolder.json");

    public bool IsWatching => _watcher?.EnableRaisingEvents ?? false;
    public string FolderPath => _folderPath;

    public event EventHandler<string>? FileDetected;

    public WatchFolderService(ILogger<WatchFolderService> logger)
    {
        _logger = logger;
        LoadConfig();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!string.IsNullOrEmpty(_folderPath) && Directory.Exists(_folderPath))
            StartWatching();
        return Task.CompletedTask;
    }

    public void StartWatching(string? path = null)
    {
        if (!string.IsNullOrEmpty(path))
        {
            _folderPath = path;
            SaveConfig();
        }

        if (string.IsNullOrEmpty(_folderPath) || !Directory.Exists(_folderPath))
        {
            _logger.LogWarning("Watch folder not configured or not found");
            return;
        }

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(_folderPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        var supported = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".gif", ".webp" };

        _watcher.Created += (_, e) =>
        {
            var ext = Path.GetExtension(e.Name)?.ToLowerInvariant();
            if (ext != null && supported.Contains(ext))
            {
                _logger.LogInformation("New file detected: {File}", e.FullPath);
                FileDetected?.Invoke(this, e.FullPath);
            }
        };

        _logger.LogInformation("Watching folder: {Folder}", _folderPath);
    }

    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
        _logger.LogInformation("Stopped watching");
    }

    private void SaveConfig()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { folderPath = _folderPath });
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, json);
    }

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = System.Text.Json.JsonSerializer.Deserialize<WatchFolderConfig>(json);
                _folderPath = config?.FolderPath ?? string.Empty;
            }
        }
        catch { /* ignore */ }
    }

    private record WatchFolderConfig(string? FolderPath);
}
