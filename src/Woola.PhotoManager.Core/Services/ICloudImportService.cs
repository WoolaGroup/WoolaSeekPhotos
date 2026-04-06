namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// Servicio para importar fotos desde un proveedor de nube sincronizado localmente
/// (actualmente: Google Drive Desktop). Flujo unidireccional: Cloud → Local.
/// </summary>
public interface ICloudImportService
{
    /// <summary>
    /// Detecta la ruta local de la carpeta sincronizada de Google Drive.
    /// Prueba cuatro estrategias: DriveInfo, Registry, rutas hardcodeadas, null.
    /// </summary>
    string? DetectGoogleDrivePath();

    /// <summary>
    /// Escanea recursivamente <paramref name="rootPath"/> devolviendo todas las
    /// entradas de fotos con extensiones soportadas.
    /// </summary>
    Task<IReadOnlyList<CloudPhotoEntry>> ScanFolderAsync(
        string rootPath,
        IProgress<string>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Copia las fotos seleccionadas a <paramref name="destinationRoot"/> y luego
    /// invoca el pipeline de indexación sobre esa carpeta.
    /// </summary>
    Task<CloudImportResult> ImportPhotosAsync(
        IEnumerable<CloudPhotoEntry> selected,
        string destinationRoot,
        IProgress<CloudImportProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>Representa una foto encontrada en la carpeta cloud local.</summary>
public record CloudPhotoEntry(
    string SourcePath,
    string RelativePath,
    long FileSizeBytes,
    DateTime LastModified);

/// <summary>Resultado de una operación de importación.</summary>
public record CloudImportResult(int Copied, int Skipped, int Total);

/// <summary>Progreso en tiempo real durante la importación.</summary>
public class CloudImportProgress
{
    public int    Total   { get; set; }
    public int    Done    { get; set; }
    public string Current { get; set; } = string.Empty;

    /// <summary>"Copiando" o "Indexando"</summary>
    public string Phase   { get; set; } = string.Empty;
}
