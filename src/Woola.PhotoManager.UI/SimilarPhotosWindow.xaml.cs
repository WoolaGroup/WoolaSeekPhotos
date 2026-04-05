using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.UI;

/// <summary>
/// IMP-009: Ventana que muestra fotos similares a la seleccionada.
/// Doble clic → abre la foto en el explorador del sistema.
/// </summary>
public partial class SimilarPhotosWindow : Window
{
    private readonly ISimilarPhotosService _service;
    private readonly int _photoId;

    public SimilarPhotosWindow(ISimilarPhotosService service, int photoId, string photoName)
    {
        InitializeComponent();
        _service = service;
        _photoId = photoId;
        TitleText.Text = $"🔍 Fotos similares a \"{photoName}\"";
        Loaded += async (_, _) => await LoadSimilarPhotosAsync();
    }

    private async Task LoadSimilarPhotosAsync()
    {
        StatusText.Text = "Buscando fotos similares...";

        try
        {
            var results = await _service.FindSimilarAsync(_photoId, limit: 12);

            if (results.Count == 0)
            {
                StatusText.Text = "No se encontraron fotos similares (se requiere modelo de embeddings).";
                return;
            }

            var viewModels = results.Select(r => new SimilarPhotoViewModel(r.Photo, r.Similarity)).ToList();
            PhotosGrid.ItemsSource = viewModels;
            StatusText.Text = $"{results.Count} fotos encontradas";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void Photo_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2) return;
        var border = sender as System.Windows.Controls.Border;
        if (border?.DataContext is SimilarPhotoViewModel vm && System.IO.File.Exists(vm.Photo.Path))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = vm.Photo.Path,
                    UseShellExecute = true
                });
            }
            catch { /* Ignorar si no puede abrir */ }
        }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Close();
}

// ── ViewModel ────────────────────────────────────────────────────────────────

public class SimilarPhotoViewModel
{
    public Photo Photo { get; }
    public string ThumbnailPath { get; }
    public string FileName => System.IO.Path.GetFileName(Photo.Path);
    public double SimilarityPercent => Math.Round(Similarity * 100, 0);
    public string SimilarityLabel => $"{SimilarityPercent:F0}%";
    public float Similarity { get; }

    public SimilarPhotoViewModel(Photo photo, float similarity)
    {
        Photo      = photo;
        Similarity = similarity;
        ThumbnailPath = !string.IsNullOrEmpty(photo.ThumbnailPath) && System.IO.File.Exists(photo.ThumbnailPath)
            ? photo.ThumbnailPath
            : photo.Path;
    }
}
