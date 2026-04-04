using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Core.Agents;
using Woola.PhotoManager.Core.Agents.Agents;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Database;
using Woola.PhotoManager.Infrastructure.Repositories;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

public partial class MainWindow : Window
{
    private IPhotoIndexer? _photoIndexer;
    private PhotoRepository? _photoRepository;
    private TagRepository? _tagRepository;
    private ISemanticSearchService? _semanticSearchService;
    private CancellationTokenSource? _indexingCts;
    private ObservableCollection<PhotoViewModel> _photos = new();
    private bool _isSemanticSearch = false;

    public MainWindow()
    {
        InitializeComponent();
        PhotoGrid.ItemsSource = _photos;
        InitializeServices();
        LoadPhotoCount();
        LoadExistingPhotos();
        LoadTags();

        FilterAllBtn.Click += FilterAllBtn_Click;
        FilterRecentBtn.Click += FilterRecentBtn_Click;
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
        var faceRepository = new FaceRepository(connectionFactory);

        var thumbnailService = new ThumbnailService();
        var metadataService = new MetadataService();
        var objectDetectionService = new ObjectDetectionService();
        var ocrService = new OcrService();
        var faceService = new FaceService();

        var textEmbeddingService = new TextEmbeddingService();
        _semanticSearchService = new SemanticSearchService(_photoRepository, _tagRepository, textEmbeddingService);

        var metadataAgent = new MetadataAgent(metadataService);
        var autoTaggingAgent = new AutoTaggingAgent(_tagRepository);
        var visionAgent = new VisionAgent(objectDetectionService);
        var ocrAgent = new OcrAgent(ocrService);
        var faceAgent = new FaceAgent(faceService, faceRepository, _tagRepository);

        var orchestrator = new AgentOrchestrator(_tagRepository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentOrchestrator>.Instance);
        orchestrator.RegisterAgent(metadataAgent);
        orchestrator.RegisterAgent(autoTaggingAgent);
        orchestrator.RegisterAgent(visionAgent);
        orchestrator.RegisterAgent(ocrAgent);
        orchestrator.RegisterAgent(faceAgent);

        _photoIndexer = new PhotoIndexer(_photoRepository, _tagRepository, thumbnailService, metadataService, orchestrator);
        _photoIndexer.ProgressChanged += OnIndexingProgress;
    }

    private async void LoadExistingPhotos()
    {
        if (_photoRepository == null) return;

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
                PhotoCountText.Text = _photos.Count.ToString();
                PhotoCountStatus.Text = $"{_photos.Count} fotos";
                StatusText.Text = $"{_photos.Count} fotos cargadas";
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private async void LoadPhotoCount()
    {
        if (_photoRepository == null) return;
        var count = await _photoRepository.GetTotalCountAsync();
        Dispatcher.Invoke(() =>
        {
            PhotoCountText.Text = count.ToString();
        });
    }

    private async Task LoadPhotosAsync()
    {
        if (_photoRepository == null) return;

        var photos = await _photoRepository.GetPhotosAsync(limit: 1000);
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
            StatusText.Text = $"{_photos.Count} fotos cargadas";
        });
    }

    private async void LoadTags()
    {
        if (_tagRepository == null) return;

        try
        {
            var tags = await _tagRepository.GetAllTagsAsync();
            var topTags = tags.Take(20);

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

    private async void SelectFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_photoIndexer == null)
        {
            MessageBox.Show("Error: Servicio no inicializado", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

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
                await _photoIndexer.StartIndexingAsync(folderPath, _indexingCts.Token);
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
        if (_isSemanticSearch) return;

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
            PhotoCountStatus.Text = $"{_photos.Count} resultados";
        });
    }

    private async void FilterAllBtn_Click(object sender, RoutedEventArgs e)
    {
        _isSemanticSearch = false;
        SearchModeIndicator.Visibility = Visibility.Collapsed;
        await LoadPhotosAsync();
        StatusText.Text = $"Mostrando todas las fotos ({_photos.Count})";
    }

    private async void FilterRecentBtn_Click(object sender, RoutedEventArgs e)
    {
        _isSemanticSearch = false;
        SearchModeIndicator.Visibility = Visibility.Collapsed;

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

    private async void TagButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as System.Windows.Controls.Button;
        var tagName = button?.CommandParameter?.ToString();

        if (string.IsNullOrEmpty(tagName)) return;

        _isSemanticSearch = false;
        SearchModeIndicator.Visibility = Visibility.Collapsed;

        try
        {
            var photos = await _tagRepository.GetPhotosByTagAsync(tagName, limit: 500);
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
        }
    }

    private async void SemanticSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        var query = SearchBox.Text.Trim();

        if (string.IsNullOrEmpty(query))
        {
            StatusText.Text = "Escribe algo para buscar semánticamente...";
            return;
        }

        if (_semanticSearchService == null)
        {
            StatusText.Text = "Servicio de búsqueda semántica no disponible";
            return;
        }

        StatusText.Text = $"🧠 Buscando semánticamente: '{query}'...";
        ProgressBar.IsIndeterminate = true;
        SemanticSearchBtn.IsEnabled = false;

        try
        {
            var results = await _semanticSearchService.SearchAsync(query, limit: 200);

            Dispatcher.Invoke(() =>
            {
                _photos.Clear();
                foreach (var photo in results)
                {
                    _photos.Add(new PhotoViewModel(photo));
                }

                _isSemanticSearch = true;
                SearchModeIndicator.Visibility = Visibility.Visible;
                SearchModeText.Text = $"Búsqueda semántica: '{query}' - {_photos.Count} resultados";
                PhotoCountStatus.Text = $"{_photos.Count} resultados semánticos";
                StatusText.Text = $"🧠 Búsqueda semántica completada: {_photos.Count} resultados";
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error en búsqueda semántica: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressBar.IsIndeterminate = false;
            SemanticSearchBtn.IsEnabled = true;
        }
    }

    private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
    {
        _isSemanticSearch = false;
        SearchModeIndicator.Visibility = Visibility.Collapsed;
        SearchBox.Text = "";
        _ = LoadPhotosAsync();
        StatusText.Text = "Búsqueda semántica cancelada";
    }

    private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SemanticSearchBtn_Click(sender, e);
        }
    }

    private void PresentationBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_photos.Count == 0)
        {
            MessageBox.Show("No hay fotos para mostrar", "Modo Presentación", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var imagePaths = _photos.Select(p => p.ThumbnailPath).Where(p => !string.IsNullOrEmpty(p)).ToList();
        var presentationWindow = new PresentationWindow(imagePaths!);
        presentationWindow.Owner = this;
        presentationWindow.ShowDialog();
    }

    // Botón: Reprocesar Tags
    private async void ReprocessTagsBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("¿Reprocesar tags automáticos para TODAS las fotos?",
                                      "Reprocesar Tags", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        StatusText.Text = "Reprocesando tags...";
        ProgressBar.IsIndeterminate = true;
        ReprocessTagsBtn.IsEnabled = false;

        try
        {
            var allPhotos = await _photoRepository.GetPhotosAsync(limit: 10000);
            var processed = 0;
            var total = allPhotos.Count();

            var connectionFactory = new SqliteConnectionFactory(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Woola", "woola.db"));
            var autoTaggingService = new AutoTaggingService(_tagRepository, connectionFactory);

            foreach (var photo in allPhotos)
            {
                await autoTaggingService.UpdateTagsForExistingPhotoAsync(photo.Id, photo);
                processed++;

                if (processed % 50 == 0)
                {
                    StatusText.Text = $"Reprocesando tags: {processed}/{total} fotos";
                    await Task.Delay(10);
                }
            }

            StatusText.Text = $"Tags reprocesados: {processed} fotos";
            MessageBox.Show($"Tags reprocesados correctamente para {processed} fotos.",
                            "Completado", MessageBoxButton.OK, MessageBoxImage.Information);

            LoadTags();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressBar.IsIndeterminate = false;
            ReprocessTagsBtn.IsEnabled = true;
        }
    }

    // Botón: Probar VisionAgent
    private void TestVisionBtn_Click(object sender, RoutedEventArgs e)
    {
        var testWindow = new TestVisionWindow();
        testWindow.Owner = this;
        testWindow.ShowDialog();
    }

    // Botón: Probar OCR Agent
    private void TestOcrBtn_Click(object sender, RoutedEventArgs e)
    {
        var testWindow = new TestOcrWindow();
        testWindow.Owner = this;
        testWindow.ShowDialog();
    }

    // Botón: Gestionar Rostros
    private void FaceManagementBtn_Click(object sender, RoutedEventArgs e)
    {
        var faceWindow = new FaceManagementWindow();
        faceWindow.Owner = this;
        faceWindow.ShowDialog();
    }
}

public class PhotoViewModel
{
    private readonly Photo _photo;

    public PhotoViewModel(Photo photo)
    {
        _photo = photo;
    }

    public string ThumbnailPath => _photo.ThumbnailPath ?? "";
    public string FileName => _photo.FileName;
    public DateTime? DateTaken => _photo.DateTaken;
    public string CameraModel => _photo.CameraModel ?? "📷 Cámara desconocida";
}