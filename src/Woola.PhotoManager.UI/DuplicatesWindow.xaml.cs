using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

/// <summary>
/// IMP-005: Ventana para detectar y gestionar fotos duplicadas.
/// </summary>
public partial class DuplicatesWindow : Window
{
    private readonly PhotoRepository _photoRepository;
    private readonly DuplicateDetectionService _detectionService;
    private readonly ObservableCollection<DuplicateGroupViewModel> _groups = new();

    public DuplicatesWindow(PhotoRepository photoRepository)
    {
        InitializeComponent();
        _photoRepository  = photoRepository;
        _detectionService = new DuplicateDetectionService(_photoRepository);
        GroupsList.ItemsSource = _groups;
    }

    private async void AnalyzeBtn_Click(object sender, RoutedEventArgs e)
    {
        AnalyzeBtn.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.IsIndeterminate = true;
        StatusText.Text = "Analizando fotos...";
        _groups.Clear();
        SummaryText.Text = string.Empty;

        try
        {
            var progress = new Progress<(int Current, int Total)>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (p.Total > 0)
                    {
                        ProgressBar.IsIndeterminate = false;
                        ProgressBar.Maximum = p.Total;
                        ProgressBar.Value   = p.Current;
                        StatusText.Text = $"Analizando: {p.Current}/{p.Total} fotos...";
                    }
                });
            });

            var duplicates = await _detectionService.FindDuplicatesAsync(progress);

            Dispatcher.Invoke(() =>
            {
                foreach (var group in duplicates)
                {
                    var vm = new DuplicateGroupViewModel(group);
                    _groups.Add(vm);
                }

                var totalDupes = duplicates.Sum(g => g.Duplicates.Count);
                StatusText.Text = $"Análisis completado.";
                SummaryText.Text = duplicates.Count > 0
                    ? $"{duplicates.Count} grupos encontrados · {totalDupes} posibles duplicados"
                    : "No se encontraron duplicados. ✓";

                ProgressBar.Visibility = Visibility.Collapsed;
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            ProgressBar.Visibility = Visibility.Collapsed;
        }
        finally
        {
            AnalyzeBtn.IsEnabled = true;
        }
    }

    private async void PhotoAction_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        if (btn?.Tag is not PhotoItemViewModel photoVm) return;

        if (photoVm.IsReference)
        {
            MessageBox.Show("Esta es la foto de referencia (original). No se puede marcar como duplicado.",
                            "Referencia", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await _photoRepository.UpdatePhotoStatusAsync(photoVm.Photo.Id, "Duplicate");
            photoVm.ActionLabel = "✓ Marcada";
            photoVm.ActionBg    = new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 100, 53));
            MessageBox.Show($"'{photoVm.FileName}' marcada como duplicado.\n" +
                            "No se eliminó ningún archivo del disco.",
                            "Marcada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void IgnoreGroup_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as System.Windows.Controls.Button;
        if (btn?.Tag is not DuplicateGroupViewModel groupVm) return;
        _groups.Remove(groupVm);
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
        => Close();
}

// ── ViewModels ───────────────────────────────────────────────────────────────

public class DuplicateGroupViewModel
{
    public string GroupLabel { get; }
    public List<PhotoItemViewModel> Photos { get; }

    public DuplicateGroupViewModel(DuplicateGroup group)
    {
        GroupLabel = $"GRUPO · {group.Duplicates.Count + 1} fotos similares";
        Photos = new List<PhotoItemViewModel>
        {
            new PhotoItemViewModel(group.Reference, isReference: true)
        };
        Photos.AddRange(group.Duplicates.Select(p => new PhotoItemViewModel(p, isReference: false)));
    }
}

public class PhotoItemViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public Photo Photo { get; }
    public bool IsReference { get; }
    public string ThumbnailPath { get; }
    public string FileName => System.IO.Path.GetFileName(Photo.Path);
    public string DateInfo => Photo.DateTaken?.ToString("dd/MM/yyyy") ?? Photo.CreatedAt.ToString("dd/MM/yyyy");
    public string SizeInfo
    {
        get
        {
            try
            {
                var fi = new System.IO.FileInfo(Photo.Path);
                return fi.Exists ? $"{fi.Length / 1024.0 / 1024.0:F1} MB" : "—";
            }
            catch { return "—"; }
        }
    }

    private string _actionLabel;
    public string ActionLabel
    {
        get => _actionLabel;
        set { _actionLabel = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ActionLabel))); }
    }

    private System.Windows.Media.Brush _actionBg;
    public System.Windows.Media.Brush ActionBg
    {
        get => _actionBg;
        set { _actionBg = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ActionBg))); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public PhotoItemViewModel(Photo photo, bool isReference)
    {
        Photo    = photo;
        IsReference = isReference;
        ThumbnailPath = !string.IsNullOrEmpty(photo.ThumbnailPath) && System.IO.File.Exists(photo.ThumbnailPath)
            ? photo.ThumbnailPath
            : photo.Path;

        if (isReference)
        {
            _actionLabel = "✓ Original";
            _actionBg    = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0, 100, 60));
        }
        else
        {
            _actionLabel = "⊘ Marcar duplicado";
            _actionBg    = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(60, 30, 30));
        }
    }
}
