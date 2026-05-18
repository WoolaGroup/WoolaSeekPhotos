using System.IO.Compression;
using Woola.PhotoManager.Backend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Woola.PhotoManager.Backend.WebApi.Services;

public class BackupOptions
{
    public bool Enabled { get; set; }
    public string FolderPath { get; set; } = string.Empty;
    public int IntervalHours { get; set; } = 24;
    public int MaxBackups { get; set; } = 7;
}

public class BackupScheduleService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BackupScheduleService> _logger;
    private BackupOptions _options = new();

    public bool IsEnabled => _options.Enabled;
    public string StatusMessage { get; private set; } = "Not configured";
    public DateTime? LastBackup { get; private set; }

    public BackupScheduleService(IServiceProvider services, ILogger<BackupScheduleService> logger)
    {
        _services = services;
        _logger = logger;
        LoadOptions();
    }

    public void UpdateOptions(BackupOptions options)
    {
        _options = options;
        SaveOptions();
        _logger.LogInformation("Backup options updated: enabled={Enabled}, interval={Interval}h, folder={Folder}, max={Max}",
            options.Enabled, options.IntervalHours, options.FolderPath, options.MaxBackups);
    }

    public BackupOptions GetOptions() => new()
    {
        Enabled = _options.Enabled,
        FolderPath = _options.FolderPath,
        IntervalHours = _options.IntervalHours,
        MaxBackups = _options.MaxBackups
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_options.Enabled && !string.IsNullOrEmpty(_options.FolderPath) && Directory.Exists(_options.FolderPath))
            {
                try
                {
                    await PerformBackupAsync(stoppingToken);
                    StatusMessage = $"Backup completed at {DateTime.UtcNow:g}";
                }
                catch (Exception ex) { _logger.LogError(ex, "Scheduled backup failed"); StatusMessage = $"Backup failed: {ex.Message}"; }
            }

            var delay = _options.Enabled ? TimeSpan.FromHours(_options.IntervalHours) : TimeSpan.FromHours(24);
            await Task.Delay(delay, stoppingToken);
        }
    }

    public async Task PerformBackupAsync(CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WoolaDbContext>();
        var woolaDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoolaPhotos");
        var dbPath = Path.Combine(woolaDir, "photos.db");
        var thumbDir = Path.Combine(woolaDir, "thumbnails");
        var backupDir = _options.FolderPath;

        Directory.CreateDirectory(backupDir);
        var backupFile = Path.Combine(backupDir, $"woola-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");

        await Task.Run(() =>
        {
            using var archive = ZipFile.Open(backupFile, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(dbPath, "photos.db");
            if (Directory.Exists(thumbDir))
                foreach (var f in Directory.GetFiles(thumbDir, "*.jpg"))
                    archive.CreateEntryFromFile(f, $"thumbnails/{Path.GetFileName(f)}");
        }, ct);

        // Clean old backups
        var backups = Directory.GetFiles(backupDir, "woola-backup-*.zip").OrderByDescending(f => f).ToList();
        while (backups.Count > _options.MaxBackups)
        {
            System.IO.File.Delete(backups.Last());
            backups.RemoveAt(backups.Count - 1);
        }

        LastBackup = DateTime.UtcNow;
        _logger.LogInformation("Backup saved: {File} ({Size} KB)", backupFile, new FileInfo(backupFile).Length / 1024);
    }

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoolaPhotos", "backup-config.json");

    private void SaveOptions()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_options);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        System.IO.File.WriteAllText(ConfigPath, json);
    }

    private void LoadOptions()
    {
        try { if (System.IO.File.Exists(ConfigPath)) _options = System.Text.Json.JsonSerializer.Deserialize<BackupOptions>(System.IO.File.ReadAllText(ConfigPath)) ?? new(); }
        catch { }
    }
}
