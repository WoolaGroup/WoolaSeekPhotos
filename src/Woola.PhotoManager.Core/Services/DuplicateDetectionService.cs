using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// IMP-005: Detección de fotos duplicadas/muy similares mediante pHash (Perceptual Hash).
///
/// Algoritmo:
///   1. Resize a 32×32 grayscale.
///   2. DCT 2D sobre la luminancia.
///   3. Tomar coeficientes top-left 8×8 (64 valores, excl. DC [0,0]).
///   4. Media de esos 64 coeficientes.
///   5. Hash de 64 bits: bit_i = coef_i >= media ? 1 : 0.
///   6. Hamming distance ≤ 10 → duplicado (~85% similitud).
///   7. Union-Find para agrupar todos los duplicados de una foto.
/// </summary>
public class DuplicateDetectionService : IDuplicateDetectionService
{
    private readonly PhotoRepository _photoRepository;

    private const int HashSize = 8;           // 8×8 = 64 bits
    private const int DctSize = 32;           // resize a 32×32 antes del DCT
    private const int HammingThreshold = 10;  // ≤10 bits diferentes → duplicado

    public DuplicateDetectionService(PhotoRepository photoRepository)
    {
        _photoRepository = photoRepository;
    }

    public async Task<List<DuplicateGroup>> FindDuplicatesAsync(
        IProgress<(int Current, int Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var allPhotos = (await _photoRepository.GetPhotosAsync(limit: 50_000)).ToList();
        var total = allPhotos.Count;

        // ── Paso 1: Computar pHash de cada foto ──────────────────────────
        var hashes = new List<(Photo Photo, ulong Hash)>(total);
        for (int i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report((i + 1, total));

            var photo = allPhotos[i];
            // Usar thumbnail si existe (más rápido), si no el path original
            var imgPath = !string.IsNullOrEmpty(photo.ThumbnailPath) && File.Exists(photo.ThumbnailPath)
                ? photo.ThumbnailPath
                : photo.Path;

            if (!File.Exists(imgPath)) continue;

            try
            {
                var hash = ComputePHash(imgPath);
                hashes.Add((photo, hash));
            }
            catch
            {
                // Ignorar fotos corruptas o inaccesibles
            }
        }

        // ── Paso 2: Union-Find para agrupar duplicados ───────────────────
        int n = hashes.Count;
        var parent = Enumerable.Range(0, n).ToArray();

        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]]; // path compression
                x = parent[x];
            }
            return x;
        }

        void Union(int x, int y)
        {
            int px = Find(x), py = Find(y);
            if (px != py) parent[px] = py;
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (HammingDistance(hashes[i].Hash, hashes[j].Hash) <= HammingThreshold)
                    Union(i, j);
            }
        }

        // ── Paso 3: Construir grupos ──────────────────────────────────────
        var groups = hashes
            .Select((h, idx) => (h.Photo, Root: Find(idx)))
            .GroupBy(x => x.Root)
            .Where(g => g.Count() > 1)
            .Select(g =>
            {
                // El más antiguo (DateTaken o CreatedAt) es la referencia
                var sorted = g.OrderBy(x => x.Photo.DateTaken ?? x.Photo.CreatedAt).ToList();
                return new DuplicateGroup(
                    Reference: sorted[0].Photo,
                    Duplicates: sorted.Skip(1).Select(x => x.Photo).ToList());
            })
            .OrderByDescending(g => g.Duplicates.Count)
            .ToList();

        return groups;
    }

    // ── pHash ──────────────────────────────────────────────────────────────

    private static ulong ComputePHash(string imagePath)
    {
        using var image = Image.Load<L8>(imagePath);

        // Resize a DctSize × DctSize grayscale
        image.Mutate(ctx => ctx.Resize(DctSize, DctSize));

        // Extraer luminancia como double[][]
        var pixels = new double[DctSize, DctSize];
        for (int y = 0; y < DctSize; y++)
            for (int x = 0; x < DctSize; x++)
                pixels[y, x] = image[x, y].PackedValue; // L8: 0-255

        // DCT 2D
        var dct = ApplyDct2D(pixels);

        // Tomar top-left HashSize×HashSize (excluyendo DC)
        var topLeft = new double[HashSize * HashSize];
        int idx = 0;
        for (int y = 0; y < HashSize; y++)
            for (int x = 0; x < HashSize; x++)
            {
                if (x == 0 && y == 0) { topLeft[idx++] = 0; continue; } // excluir DC
                topLeft[idx++] = dct[y, x];
            }

        // Media
        var mean = topLeft.Average();

        // Generar hash de 64 bits
        ulong hash = 0;
        for (int i = 0; i < 64; i++)
            if (topLeft[i] >= mean)
                hash |= (1UL << i);

        return hash;
    }

    private static double[,] ApplyDct2D(double[,] input)
    {
        int n = input.GetLength(0);
        var output = new double[n, n];
        double sqrt2N = Math.Sqrt(2.0 / n);

        for (int u = 0; u < n; u++)
        {
            double cu = u == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
            for (int v = 0; v < n; v++)
            {
                double cv = v == 0 ? 1.0 / Math.Sqrt(2) : 1.0;
                double sum = 0;
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        sum += input[i, j]
                               * Math.Cos((2 * i + 1) * u * Math.PI / (2 * n))
                               * Math.Cos((2 * j + 1) * v * Math.PI / (2 * n));
                output[u, v] = (2.0 / n) * cu * cv * sum;
            }
        }
        return output;
    }

    private static int HammingDistance(ulong a, ulong b)
        => BitOperations.PopCount(a ^ b);
}
