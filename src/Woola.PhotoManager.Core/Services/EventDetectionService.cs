using System.Globalization;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// G3: Detecta eventos temporales agrupando fotos por proximidad de fechas.
/// Algoritmo: clustering greedy — si el gap entre días consecutivos supera GapDays,
/// se inicia un nuevo evento.
/// </summary>
public class EventDetectionService : IEventDetectionService
{
    private readonly PhotoRepository _photoRepository;

    private const int GapDays  = 2;   // días de silencio para separar eventos
    private const int MinPhotos = 2;   // fotos mínimas para que cuente como evento
    private const int MaxEvents = 20;  // máximo de eventos a devolver

    private static readonly CultureInfo _es = new("es-ES");

    public EventDetectionService(PhotoRepository photoRepository)
    {
        _photoRepository = photoRepository;
    }

    public async Task<List<EventInfo>> DetectEventsAsync()
    {
        // Un elemento por foto (con repetición si múltiples fotos tienen la misma fecha)
        var allDates = (await _photoRepository.GetAllDateTakenAsync())
            .Select(d => d.Date)
            .OrderBy(d => d)
            .ToList();

        if (allDates.Count == 0) return [];

        // ── Clustering greedy ──────────────────────────────────────────────────
        var clusters = new List<(DateTime Start, DateTime End, int Count)>();

        var clusterStart = allDates[0];
        var prevDay      = allDates[0];
        int count        = 1;

        for (int i = 1; i < allDates.Count; i++)
        {
            var gap = (allDates[i] - prevDay).TotalDays;

            if (gap > GapDays)
            {
                // Cerrar clúster actual e iniciar uno nuevo
                clusters.Add((clusterStart, prevDay, count));
                clusterStart = allDates[i];
                count        = 0;
            }

            prevDay = allDates[i];
            count++;
        }
        clusters.Add((clusterStart, prevDay, count)); // último clúster

        // ── Filtrar, ordenar y retornar ────────────────────────────────────────
        return clusters
            .Where(c => c.Count >= MinPhotos)
            .OrderByDescending(c => c.Start)
            .Take(MaxEvents)
            .Select(c => new EventInfo(
                Name:       FormatEventName(c.Start, c.End),
                Start:      c.Start,
                End:        c.End.AddDays(1),   // exclusivo para queries SQL
                PhotoCount: c.Count))
            .ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatEventName(DateTime start, DateTime end)
    {
        if (start == end)
            return start.ToString("d 'de' MMMM yyyy", _es);

        if (start.Month == end.Month && start.Year == end.Year)
            return $"{start.Day}–{end.Day} {start.ToString("MMMM yyyy", _es)}";

        if (start.Year == end.Year)
            return $"{start.ToString("d MMM", _es)} – {end.ToString("d MMM yyyy", _es)}";

        return $"{start.ToString("d MMM yyyy", _es)} – {end.ToString("d MMM yyyy", _es)}";
    }
}
