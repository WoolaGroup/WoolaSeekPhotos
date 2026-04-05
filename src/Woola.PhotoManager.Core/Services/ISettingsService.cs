namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// IMP-010: Servicio de configuración persistente.
/// </summary>
public interface ISettingsService
{
    /// <summary>Carga la configuración desde disco. Devuelve defaults si el archivo no existe.</summary>
    AppSettings Load();

    /// <summary>Persiste la configuración en disco (JSON).</summary>
    void Save(AppSettings settings);
}
