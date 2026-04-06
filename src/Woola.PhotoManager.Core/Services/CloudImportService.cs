using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.IO;

namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// Implementación de <see cref="ICloudImportService"/> para Google Drive Desktop.
/// Detecta la carpeta local sincronizada sin OAuth. Flujo: copiar fotos → indexar.
/// </summary>
public sealed class CloudImportService : ICloudImportService
{
    private readonly IPhotoIndexer _photoIndexer;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<CloudImportService> _logger;

    /// <summary>Extensiones reconocidas como fotos importables.</summary>
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif", ".webp", ".heic" };

    public CloudImportService(
        IPhotoIndexer photoIndexer,
        ISettingsService settingsService,
        ILogger<CloudImportService> logger)
    {
        _photoIndexer    = photoIndexer;
        _settingsService = settingsService;
        _logger          = logger;
    }

    // ── Detección de ruta Google Drive ────────────────────────────────────────

    /// <inheritdoc/>
    public string? DetectGoogleDrivePath()
    {
        // Nivel 0: override guardado en Settings
        var settings = _settingsService.Load();
        if (!string.IsNullOrWhiteSpace(settings.GoogleDrivePath) &&
            Directory.Exists(settings.GoogleDrivePath))
        {
            _logger.LogDebug("Google Drive: ruta desde settings {Path}", settings.GoogleDrivePath);
            return settings.GoogleDrivePath;
        }

        // Nivel 1: DriveInfo — unidad virtual/red cuyo VolumeLabel contiene "Google"
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if ((drive.DriveType == DriveType.Network || drive.DriveType == DriveType.Fixed) &&
                    drive.IsReady &&
                    drive.VolumeLabel.Contains("Google", StringComparison.OrdinalIgnoreCase))
                {
                    var candidate = Path.Combine(drive.RootDirectory.FullName, "Mi unidad");
                    if (Directory.Exists(candidate))
                    {
                        _logger.LogDebug("Google Drive: detectado por VolumeLabel en {Path}", candidate);
                        return candidate;
                    }
                    // Si no tiene subcarpeta "Mi unidad", intentar la raíz directamente
                    if (Directory.Exists(drive.RootDirectory.FullName))
                    {
                        _logger.LogDebug("Google Drive: detectado por VolumeLabel (root) en {Path}",
                            drive.RootDirectory.FullName);
                        return drive.RootDirectory.FullName;
                    }
                }
            }
            catch { /* unidad no preparada o sin acceso */ }
        }

        // Nivel 2: Registry HKCU\Software\Google\Drive → valor "Path"
        try
        {
#pragma warning disable CA1416   // WPF app — Windows only
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Google\Drive");
            if (key?.GetValue("Path") is string regPath && Directory.Exists(regPath))
            {
                _logger.LogDebug("Google Drive: detectado por registry en {Path}", regPath);
                return regPath;
            }
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "No se pudo leer el registro de Google Drive");
        }

        // Nivel 3: rutas hardcodeadas comunes
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                         "Google Drive", "Mi unidad"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                         "Google Drive"),
            @"G:\Mi unidad",
            @"G:\",
            @"H:\Mi unidad",
            @"H:\",
        };

        foreach (var path in candidates)
        {
            if (Directory.Exists(path))
            {
                _logger.LogDebug("Google Drive: encontrado en ruta hardcoded {Path}", path);
                return path;
            }
        }

        _logger.LogInformation("Google Drive: carpeta local no detectada");
        return null;
    }

    // ── Escaneo de carpeta ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CloudPhotoEntry>> ScanFolderAsync(
        string rootPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var results = new List<CloudPhotoEntry>();

        await Task.Run(() =>
        {
            try
            {
                foreach (var fullPath in Directory.EnumerateFiles(
                    rootPath, "*", SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();

                    var ext = Path.GetExtension(fullPath);
                    if (!SupportedExtensions.Contains(ext)) continue;

                    try
                    {
                        var info     = new FileInfo(fullPath);
                        var relative = Path.GetRelativePath(rootPath, fullPath);
                        results.Add(new CloudPhotoEntry(fullPath, relative,
                                                        info.Length, info.LastWriteTime));
                        progress?.Report(relative);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error al leer metadatos de {File}", fullPath);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al escanear carpeta {Root}", rootPath);
                throw;
            }
        }, ct);

        return results;
    }

    // ── Importación (copiar + indexar) ────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<CloudImportResult> ImportPhotosAsync(
        IEnumerable<CloudPhotoEntry> selected,
        string destinationRoot,
        IProgress<CloudImportProgress>? progress = null,
        CancellationToken ct = default)
    {
        var list    = selected.ToList();
        var total   = list.Count;
        var copied  = 0;
        var skipped = 0;

        // ── Fase 1: Copiar ─────────────────────────────────────────────────
        for (var i = 0; i < list.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var entry    = list[i];
            var destPath = Path.Combine(destinationRoot, entry.RelativePath);

            progress?.Report(new CloudImportProgress
            {
                Phase   = "Copiando",
                Total   = total,
                Done    = i,
                Current = entry.RelativePath
            });

            try
            {
                var destDir = Path.GetDirectoryName(destPath)!;
                Directory.CreateDirectory(destDir);

                if (!File.Exists(destPath))
                {
                    await Task.Run(() => File.Copy(entry.SourcePath, destPath, overwrite: false), ct);
                    copied++;
                }
                else
                {
                    skipped++;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al copiar {Src} → {Dst}", entry.SourcePath, destPath);
                skipped++;
            }
        }

        progress?.Report(new CloudImportProgress
        {
            Phase   = "Indexando",
            Total   = total,
            Done    = copied,
            Current = destinationRoot
        });

        // ── Fase 2: Indexar ────────────────────────────────────────────────
        if (!_photoIndexer.IsRunning)
        {
            await _photoIndexer.StartIndexingAsync(destinationRoot, ct);
        }

        return new CloudImportResult(copied, skipped, total);
    }
}
