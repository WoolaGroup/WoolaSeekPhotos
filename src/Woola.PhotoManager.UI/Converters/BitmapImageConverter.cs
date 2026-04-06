using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Woola.PhotoManager.UI.Converters;

/// <summary>
/// IMP-T3-006C: Convierte una ruta de archivo (string) en un BitmapImage congelado.
///
/// Beneficios vs binding directo a string:
///   BitmapCacheOption.OnLoad  — lee el archivo y cierra el handle inmediatamente.
///                               El binding directo mantiene el handle abierto.
///   DecodePixelWidth = 220    — decodifica a resolución de la tarjeta, no a resolución completa.
///                               Una foto de 4K ocupa ~46 MB decodificada; a 220px ocupa ~0.2 MB.
///   bitmap.Freeze()           — desvincula el bitmap del árbol de objetos WPF.
///                               Permite que el GC lo recoja cuando PhotoViewModel ya no existe.
/// </summary>
[ValueConversion(typeof(string), typeof(BitmapImage))]
public class BitmapImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        if (!File.Exists(path))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource        = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption      = BitmapCacheOption.OnLoad;     // cierra el file handle tras cargar
            bitmap.CreateOptions    = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.DecodePixelWidth = 220;  // tamaño de la tarjeta; evita decode a resolución completa
            bitmap.EndInit();
            bitmap.Freeze();                // permite GC desde hilo background
            return bitmap;
        }
        catch
        {
            return null;   // thumbnail ausente o corrupto → Image no muestra nada
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
