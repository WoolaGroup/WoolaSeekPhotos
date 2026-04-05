namespace Woola.PhotoManager.Core.Services;

/// <summary>IMP-008: Exporta álbumes como archivo ZIP.</summary>
public interface IExportService
{
    /// <summary>
    /// Exporta todas las fotos de un álbum junto con metadata.json al ZIP especificado.
    /// </summary>
    /// <param name="albumId">ID del álbum a exportar.</param>
    /// <param name="albumName">Nombre del álbum (usado en el nombre del ZIP).</param>
    /// <param name="destinationPath">Ruta completa del archivo ZIP a crear.</param>
    /// <param name="progress">Progreso opcional (mensaje de estado).</param>
    /// <returns>Ruta del archivo ZIP creado.</returns>
    Task<string> ExportAlbumToZipAsync(int albumId, string albumName, string destinationPath,
                                        IProgress<string>? progress = null);
}
