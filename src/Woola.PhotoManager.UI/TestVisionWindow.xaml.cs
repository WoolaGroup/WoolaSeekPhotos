using System.Windows;
using System.Windows.Media.Imaging;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Core.Agents.Agents;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

public partial class TestVisionWindow : Window
{
    private VisionAgent? _visionAgent;
    private IObjectDetectionService? _objectDetectionService;
    private string? _currentImagePath;

    public TestVisionWindow()
    {
        InitializeComponent();
        InitializeVisionAgent();
    }

    private void InitializeVisionAgent()
    {
        _objectDetectionService = new ObjectDetectionService();
        _visionAgent = new VisionAgent(_objectDetectionService);
        StatusText.Text = "VisionAgent inicializado. Selecciona una imagen.";
    }

    private async void SelectImageBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Seleccionar imagen",
            Filter = "Imagenes|*.jpg;*.jpeg;*.png;*.bmp"
        };

        if (dialog.ShowDialog() == true)
        {
            _currentImagePath = dialog.FileName;
            ImagePathText.Text = System.IO.Path.GetFileName(_currentImagePath);

            // Mostrar imagen
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(_currentImagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            PreviewImage.Source = bitmap;

            // Ejecutar detección
            await DetectObjects();
        }
    }

    private async Task DetectObjects()
    {
        if (string.IsNullOrEmpty(_currentImagePath) || _visionAgent == null) return;

        StatusText.Text = "🔍 Detectando objetos... (puede tomar unos segundos la primera vez)";
        SelectImageBtn.IsEnabled = false;

        try
        {
            // Crear un Photo temporal para pasar al agente
            var tempPhoto = new Woola.PhotoManager.Domain.Entities.Photo
            {
                Path = _currentImagePath,
                Id = 0
            };

            var result = await _visionAgent.ExecuteAsync(tempPhoto);

            if (result.Success && result.Tags.Any())
            {
                DetectionsList.ItemsSource = result.Tags;
                StatusText.Text = $"✅ Detección completada: {result.Tags.Count} objetos encontrados en {result.ProcessingTimeMs:F0}ms";
            }
            else
            {
                DetectionsList.ItemsSource = null;
                StatusText.Text = "⚠️ No se detectaron objetos en esta imagen";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"❌ Error: {ex.Message}";
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SelectImageBtn.IsEnabled = true;
        }
    }
}