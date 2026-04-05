using Microsoft.Extensions.Logging;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// IMP-002: Clustering facial con centroide dinámico (average-linkage).
///
/// Algoritmo:
///   1. Carga todos los rostros con embedding (byte[] → float[]).
///   2. Greedy pass: cada rostro se compara con los centroides existentes.
///      Si similitud coseno > threshold → asignar al clúster, actualizar centroide.
///      Si no → crear clúster nuevo.
///   3. Nombramiento: persona_001, persona_002… (desc. por tamaño).
///   4. Sólo actualiza rostros no confirmados por el usuario.
/// </summary>
public class FaceClusteringService : IFaceClusteringService
{
    private readonly FaceRepository _faceRepository;
    private readonly ILogger<FaceClusteringService> _logger;

    public FaceClusteringService(FaceRepository faceRepository,
                                 ILogger<FaceClusteringService> logger)
    {
        _faceRepository = faceRepository;
        _logger         = logger;
    }

    public async Task<ClusterResult> ClusterAsync(float threshold = 0.65f)
    {
        // ── Paso 1: cargar rostros con embedding ──────────────────────────────
        var allFaces = (await _faceRepository.GetFacesWithEncodingAsync())
            .Where(f => f.Encoding != null && f.Encoding.Length >= 4)
            .ToList();

        if (allFaces.Count == 0)
            return new ClusterResult(0, 0, 0);

        _logger.LogInformation("[FaceClustering] {Count} rostros con embedding cargados", allFaces.Count);

        // ── Paso 2: greedy clustering con centroide dinámico ─────────────────
        // Cada entrada: (FaceIds, centroide normalizado, tamaño)
        var clusters = new List<(List<int> FaceIds, float[] Centroid)>();

        foreach (var face in allFaces)
        {
            var emb = Deserialize(face.Encoding!);
            Normalize(emb);

            if (clusters.Count == 0)
            {
                clusters.Add((new List<int> { face.Id }, (float[])emb.Clone()));
                continue;
            }

            // Buscar clúster más cercano con similitud > threshold
            int bestIdx = -1;
            float bestSim = threshold;

            for (int i = 0; i < clusters.Count; i++)
            {
                var sim = Dot(emb, clusters[i].Centroid);   // ambos normalizados → coseno
                if (sim > bestSim) { bestSim = sim; bestIdx = i; }
            }

            if (bestIdx >= 0)
            {
                var c = clusters[bestIdx];
                c.FaceIds.Add(face.Id);
                // Actualizar centroide como media incremental
                var n = c.FaceIds.Count;
                var newCentroid = new float[emb.Length];
                for (int k = 0; k < emb.Length; k++)
                    newCentroid[k] = c.Centroid[k] * ((n - 1f) / n) + emb[k] / n;
                Normalize(newCentroid);
                clusters[bestIdx] = (c.FaceIds, newCentroid);
            }
            else
            {
                clusters.Add((new List<int> { face.Id }, (float[])emb.Clone()));
            }
        }

        // ── Paso 3: asignar PersonId (persona_001…) por tamaño desc ──────────
        var sorted = clusters.OrderByDescending(c => c.FaceIds.Count).ToList();
        var faceMap = allFaces.ToDictionary(f => f.Id);
        var updated = 0;

        for (int i = 0; i < sorted.Count; i++)
        {
            var personId = $"persona_{(i + 1):D3}";

            foreach (var faceId in sorted[i].FaceIds)
            {
                if (!faceMap.TryGetValue(faceId, out var face)) continue;
                if (face.IsUserConfirmed) continue;        // respeta asignaciones manuales
                if (face.PersonId == personId) continue;  // sin cambio necesario

                await _faceRepository.UpdatePersonIdAsync(faceId, personId);
                updated++;
            }
        }

        _logger.LogInformation(
            "[FaceClustering] {Faces} rostros → {Clusters} personas, {Updated} actualizados",
            allFaces.Count, sorted.Count, updated);

        return new ClusterResult(allFaces.Count, sorted.Count, updated);
    }

    // ── Helpers matemáticos ───────────────────────────────────────────────────

    private static float[] Deserialize(byte[] bytes)
    {
        var result = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
        return result;
    }

    private static void Normalize(float[] v)
    {
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        if (norm < 1e-8f) return;
        for (int i = 0; i < v.Length; i++) v[i] /= norm;
    }

    /// <summary>Dot product (coseno si ambos vectores están normalizados).</summary>
    private static float Dot(float[] a, float[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        var sum = 0f;
        for (int i = 0; i < len; i++) sum += a[i] * b[i];
        return sum;
    }
}
