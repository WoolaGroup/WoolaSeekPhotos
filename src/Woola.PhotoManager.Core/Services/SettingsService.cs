using System.Text.Json;

namespace Woola.PhotoManager.Core.Services;

/// <summary>
/// IMP-010: Persistencia de configuración en AppData\Local\Woola\settings.json.
/// </summary>
public class SettingsService : ISettingsService
{
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Woola",
        "settings.json");

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, _options));
        }
        catch
        {
            // No romper la app si no se puede escribir
        }
    }
}
