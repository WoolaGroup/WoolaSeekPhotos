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

    /// <summary>
    /// Ruta local de Google Drive sincronizado (null = autodetectar en cada apertura de
    /// CloudImportWindow). Se puede sobreescribir desde SettingsWindow.
    /// </summary>
    public string? GoogleDrivePath { get; set; } = null;

    /// <summary>
    /// Carpeta local destino donde se copian las fotos importadas antes de indexarlas.
    /// Por defecto: Mis Imágenes\Woola Imports.
    /// </summary>
    public string ImportDestinationPath { get; set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        "Woola Imports");

    /// <summary>
    /// A1b: API Key de Anthropic para ClaudeVisionAgent.
    /// Si es null, se usa la variable de entorno ANTHROPIC_API_KEY como fallback.
    /// </summary>
    public string? AnthropicApiKey { get; set; } = null;
}
