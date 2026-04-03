using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Core.Agents.Agents;
using Woola.PhotoManager.Domain.Entities;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

public partial class TestOcrWindow : Window
{
    private OcrAgent? _ocrAgent;
    private IOcrService? _ocrService;
    private string? _currentImagePath;

    public TestOcrWindow()
    {
        InitializeComponent();
        InitializeOcrAgent();
    }

    private void InitializeOcrAgent()
    {
        try
        {
            _ocrService = new OcrService();
            _ocrAgent = new OcrAgent(_ocrService);
            StatusText.Text = "OCR Agent inicializado. Selecciona una imagen con texto.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error inicializando OCR: {ex.Message}\n\nAsegúrate de tener los datos de lenguaje en:\n%LOCALAPPDATA%\\Woola\\TessData\\spa.traineddata",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SelectImageBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Seleccionar imagen con texto",
            Filter = "Imagenes|*.jpg;*.jpeg;*.png;*.bmp;*.tiff"
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

            // Ejecutar OCR
            await ExtractText();
        }
    }

    private async Task ExtractText()
    {
        if (string.IsNullOrEmpty(_currentImagePath) || _ocrAgent == null) return;

        StatusText.Text = "🔍 Extrayendo texto con OCR...";
        SelectImageBtn.IsEnabled = false;
        OcrText.Text = "";
        TagsList.ItemsSource = null;

        try
        {
            var tempPhoto = new Photo
            {
                Path = _currentImagePath,
                Id = 0
            };

            var result = await _ocrAgent.ExecuteAsync(tempPhoto);

            if (result.Success)
            {
                // Mostrar texto extraído
                if (result.Tags.Any(t => t.Name == "contiene_texto"))
                {
                    // Buscar tags de texto
                    var textTags = result.Tags.Where(t => t.Name.StartsWith("texto_")).ToList();

                    if (textTags.Any())
                    {
                        var extractedWords = string.Join(" ", textTags.Select(t => t.Name.Replace("texto_", "")));
                        OcrText.Text = extractedWords;
                    }
                    else
                    {
                        OcrText.Text = "Texto detectado pero sin palabras clave significativas";
                    }

                    TagsList.ItemsSource = result.Tags;
                    StatusText.Text = $"✅ OCR completado: {result.Tags.Count} tags generados en {result.ProcessingTimeMs:F0}ms";
                }
                else
                {
                    OcrText.Text = "No se detectó texto en la imagen";
                    StatusText.Text = "⚠️ No se detectó texto";
                }
            }
            else
            {
                OcrText.Text = $"Error: {result.ErrorMessage}";
                StatusText.Text = $"❌ Error: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            OcrText.Text = $"Error: {ex.Message}";
            StatusText.Text = $"❌ Error: {ex.Message}";
            MessageBox.Show($"Error en OCR: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SelectImageBtn.IsEnabled = true;
        }
    }
}