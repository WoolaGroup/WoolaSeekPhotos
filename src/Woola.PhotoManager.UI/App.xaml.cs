using System.IO;
using System.Windows;

namespace Woola.PhotoManager.UI;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Crear directorios necesarios
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Woola");

        Directory.CreateDirectory(appData);
        Directory.CreateDirectory(Path.Combine(appData, "Thumbnails"));
        Directory.CreateDirectory(Path.Combine(appData, "Logs"));
    }
}