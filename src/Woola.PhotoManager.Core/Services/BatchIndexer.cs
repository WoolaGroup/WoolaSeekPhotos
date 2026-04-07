using Woola.PhotoManager.Core.Agents;

namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// B5: Clase base abstracta para indexadores de lotes de cualquier tipo de media.
/// PhotoIndexer hereda de aquí. En el futuro: VideoIndexer, DocumentIndexer.
///
/// El flujo base: discover → dedup → load → persist → process.
/// PhotoIndexer sobreescribe StartIndexingAsync con su pipeline Channel+SemaphoreSlim optimizado.
/// </summary>
public abstract class BatchIndexer<T>
{
    /// <summary>Enumera las rutas de archivos a indexar en la carpeta raíz.</summary>
    protected abstract IAsyncEnumerable<string> DiscoverPathsAsync(
        string rootPath, CancellationToken ct);

    /// <summary>Carga o construye un item de tipo T desde su ruta.</summary>
    protected abstract Task<T?> LoadItemAsync(string path, CancellationToken ct);

    /// <summary>Retorna true si el item ya está indexado (deduplicación).</summary>
    protected abstract Task<bool> IsAlreadyIndexedAsync(string path, CancellationToken ct);

    /// <summary>Persiste el item en el almacenamiento y retorna la versión guardada.</summary>
    protected abstract Task<T> PersistItemAsync(T item, CancellationToken ct);

    /// <summary>Persiste los resultados del procesamiento (tags, análisis) para el item.</summary>
    protected abstract Task PersistResultsAsync(
        T item, IEnumerable<ProcessorTag> tags, CancellationToken ct);

    /// <summary>
    /// Flujo base: discover → dedup → load → persist → process.
    /// Las subclases pueden sobreescribir este método con implementaciones más optimizadas.
    /// </summary>
    public virtual async Task StartIndexingAsync(
        string rootPath, CancellationToken ct = default)
    {
        await foreach (var path in DiscoverPathsAsync(rootPath, ct).WithCancellation(ct))
        {
            if (ct.IsCancellationRequested) break;
            if (await IsAlreadyIndexedAsync(path, ct)) continue;

            var item = await LoadItemAsync(path, ct);
            if (item == null) continue;

            var persisted = await PersistItemAsync(item, ct);
            await PersistResultsAsync(persisted, Array.Empty<ProcessorTag>(), ct);
        }
    }
}
