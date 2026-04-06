using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using Woola.PhotoManager.Core.Services;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

/// <summary>
/// Ventana de importación de fotos desde Google Drive local.
/// Flujo: detectar ruta → escanear → filtrar → seleccionar → importar (copiar + indexar).
/// </summary>
public partial class CloudImportWindow : Window
{
    private readonly ICloudImportService _cloudImportService;
    private readonly ISettingsService    _settingsService;

    private readonly ObservableCollection<CloudPhotoViewModel> _allPhotos  = new();
    private          ICollectionView?                           _filteredView;
    private          CancellationTokenSource?                   _cts;
    private          string?                                    _currentPath;

    public CloudImportWindow(
        ICloudImportService cloudImportService,
        ISettingsService settingsService)
    {
        InitializeComponent();

        _cloudImportService = cloudImportService;
        _settingsService    = settingsService;

        // Vista filtrada para el ListView
        _filteredView = CollectionViewSource.GetDefaultView(_allPhotos);
        PhotoListView.ItemsSource = _filteredView;

        Loaded += OnLoaded;
    }

    // ── Inicialización ────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Override desde settings, luego autodetectar
        var settings    = _settingsService.Load();
        _currentPath    = settings.GoogleDrivePath ?? _cloudImportService.DetectGoogleDrivePath();
        DrivePathText.Text = _currentPath ?? "No detectado";

        if (_currentPath == null)
        {
            StatusText.Text = "⚠ Google Drive no detectado. Usa «Cambiar» para seleccionar la carpeta.";
            ScanBtn.IsEnabled = false;
        }
    }

    // ── Cambiar carpeta fuente ────────────────────────────────────────────────

    private void ChangePathBtn_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description  = "Seleccionar carpeta de Google Drive local",
            UseDescriptionForTitle = true,
            SelectedPath = _currentPath ?? string.Empty
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        _currentPath       = dialog.SelectedPath;
        DrivePathText.Text = _currentPath;
        ScanBtn.IsEnabled  = true;
        StatusText.Text    = "Carpeta seleccionada. Haz clic en «Escanear».";
    }

    // ── Escanear carpeta ──────────────────────────────────────────────────────

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentPath)) return;

        ScanBtn.IsEnabled  = false;
        ImportBtn.IsEnabled = false;
        _allPhotos.Clear();
        EmptyState.Visibility    = Visibility.Collapsed;
        PhotoListView.Visibility = Visibility.Collapsed;
        StatusText.Text          = "Escaneando...";

        try
        {
            _cts = new CancellationTokenSource();
            var progress = new Progress<string>(msg => StatusText.Text = $"Escaneando: {msg}");

            var entries = await _cloudImportService.ScanFolderAsync(
                _currentPath, progress, _cts.Token);

            // Advertencia para formatos no indexables
            var unsupported = entries.Count(e => e.RelativePath.EndsWith(".webp",
                StringComparison.OrdinalIgnoreCase) ||
                e.RelativePath.EndsWith(".heic", StringComparison.OrdinalIgnoreCase));

            foreach (var entry in entries)
                _allPhotos.Add(new CloudPhotoViewModel(entry));

            if (_allPhotos.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                StatusText.Text = "No se encontraron fotos en la carpeta seleccionada.";
            }
            else
            {
                PhotoListView.Visibility = Visibility.Visible;
                StatusText.Text = $"{_allPhotos.Count} fotos encontradas" +
                    (unsupported > 0 ? $" ({unsupported} .webp/.heic no se indexarán)" : "");
                ImportBtn.IsEnabled = true;
            }

            UpdateSelectionCount();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Escaneo cancelado.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error al escanear: {ex.Message}";
            MessageBox.Show($"Error al escanear la carpeta:\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ScanBtn.IsEnabled = true;
        }
    }

    // ── Filtro por nombre ─────────────────────────────────────────────────────

    private void FilterBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_filteredView == null) return;
        var filter = FilterBox.Text.Trim();

        _filteredView.Filter = string.IsNullOrWhiteSpace(filter)
            ? null
            : obj => obj is CloudPhotoViewModel vm &&
                     vm.FileName.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    // ── Seleccionar todo ──────────────────────────────────────────────────────

    private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vm in _allPhotos) vm.IsSelected = true;
        UpdateSelectionCount();
    }

    private void PhotoCheckBox_Changed(object sender, RoutedEventArgs e)
        => UpdateSelectionCount();

    private void UpdateSelectionCount()
    {
        var count = _allPhotos.Count(v => v.IsSelected);
        SelectionCountText.Text = $"{count} seleccionada{(count != 1 ? "s" : "")}";
        ImportBtn.IsEnabled = count > 0;
    }

    // ── Importar ──────────────────────────────────────────────────────────────

    private async void ImportBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = _allPhotos.Where(v => v.IsSelected).Select(v => v.Entry).ToList();
        if (selected.Count == 0) return;

        var settings = _settingsService.Load();
        var destRoot = settings.ImportDestinationPath;

        // Confirmar si la carpeta destino no existe y hay que crearla
        if (!Directory.Exists(destRoot))
        {
            var res = MessageBox.Show(
                $"La carpeta destino no existe:\n{destRoot}\n\n¿Crearla automáticamente?",
                "Carpeta destino", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;
            Directory.CreateDirectory(destRoot);
        }

        SetControlsEnabled(false);
        ImportProgress.Visibility = Visibility.Visible;
        ImportProgress.Maximum    = selected.Count;
        ImportProgress.Value      = 0;

        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<CloudImportProgress>(p =>
            {
                StatusText.Text      = $"{p.Phase}: {p.Current} ({p.Done}/{p.Total})";
                ImportProgress.Value = p.Done;
                if (p.Phase == "Indexando")
                    ImportProgress.IsIndeterminate = true;
            });

            var result = await _cloudImportService.ImportPhotosAsync(
                selected, destRoot, progress, _cts.Token);

            StatusText.Text      = $"✓ Completado: {result.Copied} copiadas, {result.Skipped} omitidas";
            ImportProgress.Visibility = Visibility.Collapsed;

            MessageBox.Show(
                $"Importación completada.\n\n" +
                $"Fotos copiadas:  {result.Copied}\n" +
                $"Omitidas (ya existían): {result.Skipped}\n\n" +
                $"Carpeta destino:\n{destRoot}",
                "Importación completada",
                MessageBoxButton.OK, MessageBoxImage.Information);

            DialogResult = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Importación cancelada.";
            ImportProgress.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            ImportProgress.Visibility = Visibility.Collapsed;
            MessageBox.Show($"Error durante la importación:\n{ex.Message}",
                            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetControlsEnabled(true);
        }
    }

    // ── Cancelar ──────────────────────────────────────────────────────────────

    private void CancelImportBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        DialogResult = false;
        Close();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetControlsEnabled(bool enabled)
    {
        ScanBtn.IsEnabled        = enabled;
        ImportBtn.IsEnabled      = enabled && _allPhotos.Any(v => v.IsSelected);
        FilterBox.IsEnabled      = enabled;
        CancelImportBtn.Content  = enabled ? "Cancelar" : "Detener";
    }
}

// ── ViewModel por foto ────────────────────────────────────────────────────────

/// <summary>ViewModel observable para cada fila del ListView de fotos.</summary>
public class CloudPhotoViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public CloudPhotoEntry Entry { get; }

    public string FileName           => System.IO.Path.GetFileName(Entry.SourcePath);
    public string FolderPath         => System.IO.Path.GetDirectoryName(Entry.RelativePath) ?? "";
    public string SizeDisplay        => FormatSize(Entry.FileSizeBytes);
    public string LastModifiedDisplay => Entry.LastModified.ToString("dd/MM/yyyy HH:mm");
    public string StatusDisplay       => string.Empty; // se actualiza durante importación

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CloudPhotoViewModel(CloudPhotoEntry entry) => Entry = entry;

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024              => $"{bytes} B",
        < 1024 * 1024       => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _                   => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
