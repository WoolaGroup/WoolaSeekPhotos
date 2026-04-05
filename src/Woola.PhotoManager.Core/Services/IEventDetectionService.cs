namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// Representa un clúster temporal de fotos detectado automáticamente.
/// </summary>
/// <param name="Name">Nombre legible del evento, p.ej. "15-18 junio 2024".</param>
/// <param name="Start">Primera fecha del clúster (inclusive), sin componente horario.</param>
/// <param name="End">Fecha exclusiva de fin para queries SQL (= último día + 1).</param>
/// <param name="PhotoCount">Número de fotos en el evento.</param>
public record EventInfo(string Name, DateTime Start, DateTime End, int PhotoCount);

public interface IEventDetectionService
{
    /// <summary>
    /// Detecta eventos agrupando fotos por proximidad temporal.
    /// Devuelve hasta 20 eventos ordenados de más reciente a más antiguo.
    /// </summary>
    Task<List<EventInfo>> DetectEventsAsync();
}
