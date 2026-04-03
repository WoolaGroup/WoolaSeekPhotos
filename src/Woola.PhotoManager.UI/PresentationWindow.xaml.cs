using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Woola.PhotoManager.UI;

public partial class PresentationWindow : Window
{
    private readonly List<string> _imagePaths;
    private int _currentIndex = 0;

    public PresentationWindow(List<string> imagePaths)
    {
        InitializeComponent();
        _imagePaths = imagePaths;

        if (_imagePaths.Any())
        {
            ShowCurrentImage();
        }
    }

    private void ShowCurrentImage()
    {
        if (_currentIndex >= 0 && _currentIndex < _imagePaths.Count)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(_imagePaths[_currentIndex], UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            CurrentImage.Source = bitmap;
            InfoText.Text = $"{_currentIndex + 1} / {_imagePaths.Count}";
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                if (_currentIndex > 0)
                {
                    _currentIndex--;
                    ShowCurrentImage();
                }
                break;

            case Key.Right:
                if (_currentIndex < _imagePaths.Count - 1)
                {
                    _currentIndex++;
                    ShowCurrentImage();
                }
                break;

            case Key.Escape:
                Close();
                break;
        }
    }
}