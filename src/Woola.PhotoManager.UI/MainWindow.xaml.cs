using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Database;
using Woola.PhotoManager.Infrastructure.Repositories;
using System.IO;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

public partial class MainWindow : Window
{
    private IPhotoIndexer? _photoIndexer;
    private PhotoRepository? _photoRepository;
    private CancellationTokenSource? _indexingCts;
    private ObservableCollection<PhotoViewModel> _photos = new();

    public MainWindow()
    {
        InitializeComponent();
        PhotoGrid.ItemsSource = _photos;
        InitializeServices();
        LoadPhotoCount();

        // Conectar eventos de filtros
        FilterAllBtn.Click += FilterAllBtn_Click;
        FilterRecentBtn.Click += FilterRecentBtn_Click;

        // ✅ NUEVO: Cargar fotos existentes al iniciar
        LoadExistingPhotos();
    }

    // ✅ NUEVO MÉTODO: Cargar fotos que ya están en la base de datos

    private async void LoadExistingPhotos()
    {
        if (_photoRepository == null) return;

        try
        {
            var photos = await _photoRepository.GetPhotosAsync(limit: 1000);

            // Ordenar por fecha (más reciente primero)
            var sortedPhotos = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt);

            Dispatcher.Invoke(() =>
            {
                _photos.Clear();
                foreach (var photo in sortedPhotos)
                {
                    _photos.Add(new PhotoViewModel(photo));
                }
                PhotoCountText.Text = _photos.Count.ToString();
                PhotoCountStatus.Text = $"{_photos.Count} fotos";
                StatusText.Text = $"{_photos.Count} fotos cargadas (recientes primero)";

                if (_photos.Count > 0)
                {
                    FolderPathText.Text = "Fotos cargadas desde la base de datos";
                }
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error al cargar fotos: {ex.Message}";
        }
    }

    private void InitializeServices()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Woola",
            "woola.db");

        var connectionFactory = new SqliteConnectionFactory(dbPath);
        _photoRepository = new PhotoRepository(connectionFactory);
        var tagRepository = new TagRepository(connectionFactory);
        var thumbnailService = new ThumbnailService();
        var metadataService = new MetadataService();  // ← Nuevo

        _photoIndexer = new PhotoIndexer(_photoRepository, tagRepository, thumbnailService, metadataService);
        _photoIndexer.ProgressChanged += OnIndexingProgress;
    }


    private async void SelectFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        dialog.Description = "Seleccionar carpeta de fotos";
        dialog.ShowNewFolderButton = false;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var folderPath = dialog.SelectedPath;
            FolderPathText.Text = folderPath;

            SelectFolderBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;

            _indexingCts = new CancellationTokenSource();

            try
            {
                await _photoIndexer!.StartIndexingAsync(folderPath, _indexingCts.Token);

                // ✅ Después de indexar, recargar las fotos
                await LoadPhotosAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SelectFolderBtn.IsEnabled = true;
                StopBtn.IsEnabled = false;
            }
        }
    }

    private async void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        _indexingCts?.Cancel();
        StatusText.Text = "Deteniendo...";
    }

    private void OnIndexingProgress(object? sender, IndexingProgress e)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Value = e.Percentage;
            ProgressText.Text = $"{e.Processed}/{e.TotalFound} - {e.CurrentFile}";
            StatusText.Text = e.Processed < e.TotalFound ? "Indexando..." : "Indexación completa";
        });
    }

    private async void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var searchTerm = SearchBox.Text.Trim();

        if (string.IsNullOrEmpty(searchTerm))
        {
            await LoadPhotosAsync();
        }
        else
        {
            await SearchPhotosAsync(searchTerm);
        }
    }



    private async Task LoadPhotosAsync()
    {
        if (_photoRepository == null) return;

        var photos = await _photoRepository.GetPhotosAsync(limit: 1000);

        // Ordenar por fecha (más reciente primero)
        var sortedPhotos = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt);

        Dispatcher.Invoke(() =>
        {
            _photos.Clear();
            foreach (var photo in sortedPhotos)
            {
                _photos.Add(new PhotoViewModel(photo));
            }
            PhotoCountText.Text = _photos.Count.ToString();
            PhotoCountStatus.Text = $"{_photos.Count} fotos";
            StatusText.Text = $"Listo - {_photos.Count} fotos cargadas";
        });
    }



    private async Task SearchPhotosAsync(string searchTerm)
    {
        if (_photoRepository == null) return;

        var photos = await _photoRepository.SearchPhotosAsync(searchTerm, limit: 200);

        Dispatcher.Invoke(() =>
        {
            _photos.Clear();
            foreach (var photo in photos)
            {
                _photos.Add(new PhotoViewModel(photo));
            }
            PhotoCountText.Text = $"{_photos.Count} resultados";
        });
    }

    private async void LoadPhotoCount()
    {
        if (_photoRepository == null) return;
        var count = await _photoRepository.GetTotalCountAsync();
        Dispatcher.Invoke(() =>
        {
            PhotoCountText.Text = $"{count} fotos";
        });
    }

    // Botón de Modo Presentación
    private async void PresentationBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_photos.Count == 0)
        {
            System.Windows.MessageBox.Show("No hay fotos para mostrar en modo presentación.",
                            "Sin fotos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var presentationWindow = new PresentationWindow(_photos.Select(p => p.ThumbnailPath).ToList());
        presentationWindow.ShowDialog();
    }

    // Filtro: Todas las fotos
    private async void FilterAllBtn_Click(object sender, RoutedEventArgs e)
    {
        await LoadPhotosAsync();
        StatusText.Text = $"Mostrando todas las fotos ({_photos.Count})";
    }

    // Filtro: Últimos 30 días
    private async void FilterRecentBtn_Click(object sender, RoutedEventArgs e)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        if (_photoRepository == null) return;

        var allPhotos = await _photoRepository.GetPhotosAsync(limit: 1000);
        var recentPhotos = allPhotos.Where(p => p.DateTaken >= thirtyDaysAgo || p.CreatedAt >= thirtyDaysAgo);

        Dispatcher.Invoke(() =>
        {
            _photos.Clear();
            foreach (var photo in recentPhotos)
            {
                _photos.Add(new PhotoViewModel(photo));
            }
            PhotoCountStatus.Text = $"{_photos.Count} fotos (últimos 30 días)";
            StatusText.Text = $"Filtrado: últimos 30 días - {_photos.Count} fotos";
        });
    }
}

