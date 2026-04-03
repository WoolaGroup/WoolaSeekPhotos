using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Core.Agents;
using Woola.PhotoManager.Core.Agents.Agents;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Database;
using Woola.PhotoManager.Infrastructure.Repositories;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using MessageBox = System.Windows.MessageBox;


namespace Woola.PhotoManager.UI;

public partial class MainWindow : Window
{
    private IPhotoIndexer? _photoIndexer;
    private PhotoRepository? _photoRepository;
    private TagRepository? _tagRepository;  // ← Agregar esta línea
    private CancellationTokenSource? _indexingCts;
    private ObservableCollection<PhotoViewModel> _photos = new();

    public MainWindow()
    {
        InitializeComponent();
        PhotoGrid.ItemsSource = _photos;
        InitializeServices();
        LoadPhotoCount();
        LoadExistingPhotos();
        LoadTags();  // ← Nuevo

        FilterAllBtn.Click += FilterAllBtn_Click;
        FilterRecentBtn.Click += FilterRecentBtn_Click;
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

            // Cargar tags después de cargar fotos
            LoadTags();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error al cargar fotos: {ex.Message}";
        }
    }


    private async void LoadTags()
    {
        if (_tagRepository == null) return;

        try
        {
            var tags = await _tagRepository.GetAllTagsAsync();
            var topTags = tags.Take(20); // Mostrar solo los 20 más usados

            Dispatcher.Invoke(() =>
            {
                TagsList.ItemsSource = topTags;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading tags: {ex.Message}");
        }
    }

    // Click en un tag para filtrar
    private async void TagButton_Click(object sender, RoutedEventArgs e)
    {
        Button? button = sender as Button;
        var tagName = button?.CommandParameter?.ToString();  // ← Cambiar a CommandParameter

        System.Diagnostics.Debug.WriteLine($"Tag clickeado: {tagName}");

        if (string.IsNullOrEmpty(tagName))
        {
            StatusText.Text = "Error: Tag no válido";
            return;
        }

        StatusText.Text = $"Filtrando por tag: {tagName}...";

        try
        {
            var photos = await _tagRepository.GetPhotosByTagAsync(tagName, limit: 1000);

            var sortedPhotos = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt);

            Dispatcher.Invoke(() =>
            {
                _photos.Clear();
                foreach (var photo in sortedPhotos)
                {
                    _photos.Add(new PhotoViewModel(photo));
                }
                PhotoCountStatus.Text = $"{_photos.Count} fotos con tag: {tagName}";
                StatusText.Text = $"Mostrando {_photos.Count} fotos con tag: {tagName}";
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
        }
    }

    private async void FilterAllBtn_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Cargando todas las fotos...";

        try
        {
            var photos = await _photoRepository.GetPhotosAsync(limit: 1000);
            var sortedPhotos = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt);

            Dispatcher.Invoke(() =>
            {
                _photos.Clear();
                foreach (var photo in sortedPhotos)
                {
                    _photos.Add(new PhotoViewModel(photo));
                }
                PhotoCountStatus.Text = $"{_photos.Count} fotos";
                StatusText.Text = $"Mostrando todas las fotos ({_photos.Count})";
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
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
        _tagRepository = new TagRepository(connectionFactory);

        // Servicios
        var thumbnailService = new ThumbnailService();
        var metadataService = new MetadataService();
        var objectDetectionService = new ObjectDetectionService();

        // Crear agentes
        var metadataAgent = new MetadataAgent(metadataService);
        var autoTaggingAgent = new AutoTaggingAgent(_tagRepository);
        var visionAgent = new VisionAgent(objectDetectionService);

        // Crear orquestador y registrar agentes
        var orchestrator = new AgentOrchestrator(_tagRepository);
        orchestrator.RegisterAgent(metadataAgent);
        orchestrator.RegisterAgent(autoTaggingAgent);
        orchestrator.RegisterAgent(visionAgent);

        // Crear indexador con el orquestador
        _photoIndexer = new PhotoIndexer(_photoRepository, _tagRepository, thumbnailService, metadataService, orchestrator);
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

    private async void ReprocessTagsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_photoRepository == null || _tagRepository == null)
        {
            MessageBox.Show("Servicios no inicializados", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var result = MessageBox.Show("¿Reprocesar tags para TODAS las fotos existentes?\n\nEsto puede tomar varios minutos.",
                                      "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        ReprocessTagsBtn.IsEnabled = false;
        StatusText.Text = "Reprocesando tags...";
        ProgressBar.IsIndeterminate = true;

        try
        {
            var allPhotos = await _photoRepository.GetPhotosAsync(limit: 10000);
            var connectionFactory = new Infrastructure.Database.SqliteConnectionFactory(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Woola", "woola.db"));
            var autoTaggingService = new AutoTaggingService(_tagRepository, connectionFactory);

            var processed = 0;
            var total = allPhotos.Count();

            foreach (var photo in allPhotos)
            {
                await autoTaggingService.UpdateTagsForExistingPhotoAsync(photo.Id, photo);
                processed++;

                if (processed % 100 == 0)
                {
                    StatusText.Text = $"Reprocesando tags: {processed}/{total} fotos";
                }
            }

            StatusText.Text = $"Tags reprocesados: {processed} fotos";
            MessageBox.Show($"Tags reprocesados correctamente para {processed} fotos.",
                            "Completado", MessageBoxButton.OK, MessageBoxImage.Information);

            // Recargar tags
            LoadTags();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error al reprocesar tags: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ReprocessTagsBtn.IsEnabled = true;
            ProgressBar.IsIndeterminate = false;
        }


    }

    private void TestVisionBtn_Click(object sender, RoutedEventArgs e)
    {
        var testWindow = new TestVisionWindow();
        testWindow.Owner = this;
        testWindow.ShowDialog();
    }
}

