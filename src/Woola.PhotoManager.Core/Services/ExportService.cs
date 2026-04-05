using System.IO.Compression;
using System.Text.Json;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// IMP-008: Exporta un álbum como archivo ZIP con fotos originales + metadata.json.
/// </summary>
public class ExportService : IExportService
{
    private readonly AlbumRepository _albumRepository;
    private readonly PhotoRepository _photoRepository;
    private readonly TagRepository _tagRepository;

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    public ExportService(AlbumRepository albumRepository,
                         PhotoRepository photoRepository,
                         TagRepository tagRepository)
    {
        _albumRepository = albumRepository;
        _photoRepository = photoRepository;
        _tagRepository   = tagRepository;
    }

    public async Task<string> ExportAlbumToZipAsync(
        int albumId,
        string albumName,
        string destinationPath,
        IProgress<string>? progress = null)
    {
        var photos = (await _albumRepository.GetPhotosInAlbumAsync(albumId)).ToList();
        progress?.Report($"Cargando {photos.Count} fotos...");

        // Recopilar metadata de cada foto
        var metadataList = new List<object>();
        foreach (var photo in photos)
        {
            var tags = (await _tagRepository.GetTagsForPhotoAsync(photo.Id))
                .Select(t => new { t.Name, t.Category })
                .ToList();

            metadataList.Add(new
            {
                id          = photo.Id,
                filename    = Path.GetFileName(photo.Path),
                dateTaken   = photo.DateTaken?.ToString("yyyy-MM-dd HH:mm:ss"),
                camera      = photo.CameraModel,
                lens        = photo.LensModel,
                latitude    = photo.Latitude,
                longitude   = photo.Longitude,
                width       = photo.Width,
                height      = photo.Height,
                tags
            });
        }

        // Crear el ZIP
        progress?.Report($"Creando ZIP en {destinationPath}...");

        // Asegurar directorio destino
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var zipStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);
        using var archive   = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false);

        // Añadir metadata.json
        var metaEntry  = archive.CreateEntry("metadata.json");
        using (var writer = new StreamWriter(metaEntry.Open()))
            await writer.WriteAsync(JsonSerializer.Serialize(metadataList, _jsonOpts));

        // Añadir fotos originales
        int processed = 0;
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var photo in photos)
        {
            if (!File.Exists(photo.Path)) { processed++; continue; }

            // Evitar nombres duplicados en el ZIP
            var fileName = Path.GetFileName(photo.Path);
            if (!usedNames.Add(fileName))
            {
                var stem = Path.GetFileNameWithoutExtension(fileName);
                var ext  = Path.GetExtension(fileName);
                fileName = $"{stem}_{photo.Id}{ext}";
                usedNames.Add(fileName);
            }

            var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);
            using var entryStream = entry.Open();
            using var fileStream  = File.OpenRead(photo.Path);
            await fileStream.CopyToAsync(entryStream);

            processed++;
            if (processed % 10 == 0)
                progress?.Report($"Exportando {processed}/{photos.Count}...");
        }

        progress?.Report($"ZIP creado: {Path.GetFileName(destinationPath)} ({photos.Count} fotos)");
        return destinationPath;
    }
}
