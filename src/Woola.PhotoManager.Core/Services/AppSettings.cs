namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// IMP-010: Configuración persistente de la aplicación.
/// Se serializa en AppData\Local\Woola\settings.json.
/// </summary>
public class AppSettings
{
    /// <summary>Estado enabled/disabled por nombre de agente.</summary>
    public Dictionary<string, bool> AgentEnabled { get; set; } = new();

    /// <summary>Umbral de similitud coseno para clustering facial (0.50–0.90).</summary>
    public float FaceClusterThreshold { get; set; } = 0.65f;
}
