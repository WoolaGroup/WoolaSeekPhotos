using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Services;

/// <summary>IMP-005: Detecta grupos de fotos duplicadas/muy similares por pHash.</summary>
public record DuplicateGroup(Photo Reference, List<Photo> Duplicates);

public interface IDuplicateDetectionService
{
    /// <summary>
    /// Analiza todas las fotos indexadas y retorna grupos de duplicados.
    /// Algoritmo: pHash (perceptual hash) + Hamming distance ≤ 10.
    /// </summary>
    Task<List<DuplicateGroup>> FindDuplicatesAsync(IProgress<(int Current, int Total)>? progress = null,
                                                    CancellationToken cancellationToken = default);
}
