using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;
using Woola.PhotoManager.UI.Services;

namespace Woola.PhotoManager.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPhotoIndexer _photoIndexer;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly PhotoRepository _photoRepository;
    private readonly TagRepository _tagRepository;
    private readonly IAutoTaggingService _autoTaggingService;
    private readonly IFolderPickerService _folderPickerService;

    private CancellationTokenSource? _indexingCts;

    public ObservableCollection<PhotoViewModel> Photos { get; } = new();
    public ObservableCollection<Tag> Tags { get; } = new();

    // Evento para que MainWindow muestre MessageBox (UI concern)
    public event EventHandler<string>? ErrorOccurred;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartIndexingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopIndexingCommand))]
    private bool _isIndexing;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _progressText = "Listo";
    [ObservableProperty] private bool _isProgressIndeterminate;
    [ObservableProperty] private string _statusText = "Listo";
    [ObservableProperty] private string _photoCountText = "0";
    [ObservableProperty] private string _photoCountStatus = "0 fotos";
    [ObservableProperty] private string _folderPathText = "Ninguna carpeta seleccionada";
    [ObservableProperty] private bool _isSemanticModeVisible;
    [ObservableProperty] private string _semanticModeLabel = string.Empty;

    public MainViewModel(
        IPhotoIndexer photoIndexer,
        ISemanticSearchService semanticSearchService,
        PhotoRepository photoRepository,
        TagRepository tagRepository,
        IAutoTaggingService autoTaggingService,
        IFolderPickerService folderPickerService)
    {
        _photoIndexer = photoIndexer;
        _semanticSearchService = semanticSearchService;
        _photoRepository = photoRepository;
        _tagRepository = tagRepository;
        _autoTaggingService = autoTaggingService;
        _folderPickerService = folderPickerService;

        _photoIndexer.ProgressChanged += OnIndexingProgress;
        _ = InitializeAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        if (IsSemanticModeVisible) return;
        _ = string.IsNullOrEmpty(value) ? LoadPhotosAsync() : SearchPhotosAsync(value);
    }

    private async Task InitializeAsync()
    {
        await LoadPhotosAsync();
        await LoadTagsAsync();
    }

    private async Task LoadPhotosAsync()
    {
        var photos = await _photoRepository.GetPhotosAsync(limit: 1000);
        var sorted = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt);

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Photos.Clear();
            foreach (var photo in sorted)
                Photos.Add(new PhotoViewModel(photo));

            PhotoCountText = Photos.Count.ToString();
            PhotoCountStatus = $"{Photos.Count} fotos";
            StatusText = $"{Photos.Count} fotos cargadas";
        });
    }

    private async Task LoadTagsAsync()
    {
        var tags = await _tagRepository.GetAllTagsAsync();
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Tags.Clear();
            foreach (var tag in tags.Take(20))
                Tags.Add(tag);
        });
    }

    private async Task SearchPhotosAsync(string searchTerm)
    {
        var photos = await _photoRepository.SearchPhotosAsync(searchTerm, limit: 200);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Photos.Clear();
            foreach (var photo in photos)
                Photos.Add(new PhotoViewModel(photo));
            PhotoCountStatus = $"{Photos.Count} resultados";
        });
    }

    private void OnIndexingProgress(object? sender, IndexingProgress e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressValue = e.Percentage;
            ProgressText = $"{e.Processed}/{e.TotalFound} – {e.CurrentFile}";
            StatusText = e.Processed < e.TotalFound ? "Indexando..." : "Indexación completa";
        });
    }

    [RelayCommand(CanExecute = nameof(CanStartIndexing))]
    private async Task StartIndexingAsync()
    {
        var folderPath = _folderPickerService.PickFolder("Seleccionar carpeta de fotos");
        if (folderPath == null) return;

        FolderPathText = folderPath;
        IsIndexing = true;
        _indexingCts = new CancellationTokenSource();

        try
        {
            await _photoIndexer.StartIndexingAsync(folderPath, _indexingCts.Token);
            await LoadPhotosAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error durante la indexación: {ex.Message}");
        }
        finally
        {
            IsIndexing = false;
        }
    }
    private bool CanStartIndexing() => !IsIndexing;

    [RelayCommand(CanExecute = nameof(IsIndexing))]
    private void StopIndexing()
    {
        _indexingCts?.Cancel();
        StatusText = "Deteniendo...";
    }
    private bool IsIndexingCanExecute() => IsIndexing;

    [RelayCommand]
    private async Task SemanticSearchAsync()
    {
        var query = SearchText.Trim();
        if (string.IsNullOrEmpty(query))
        {
            StatusText = "Escribe algo para buscar semánticamente...";
            return;
        }

        StatusText = $"🧠 Buscando: '{query}'...";
        IsProgressIndeterminate = true;

        try
        {
            var results = await _semanticSearchService.SearchAsync(query, limit: 200);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Photos.Clear();
                foreach (var photo in results)
                    Photos.Add(new PhotoViewModel(photo));

                IsSemanticModeVisible = true;
                SemanticModeLabel = $"Búsqueda semántica: '{query}' – {Photos.Count} resultados";
                PhotoCountStatus = $"{Photos.Count} resultados semánticos";
                StatusText = $"🧠 Búsqueda semántica: {Photos.Count} resultados";
            });
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error en búsqueda semántica: {ex.Message}");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsProgressIndeterminate = false;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        IsSemanticModeVisible = false;
        SearchText = string.Empty;
        StatusText = "Búsqueda semántica cancelada";
        _ = LoadPhotosAsync();
    }

    [RelayCommand]
    private async Task FilterAllAsync()
    {
        IsSemanticModeVisible = false;
        await LoadPhotosAsync();
        StatusText = $"Mostrando todas las fotos ({Photos.Count})";
    }

    [RelayCommand]
    private async Task FilterRecentAsync()
    {
        IsSemanticModeVisible = false;
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var allPhotos = await _photoRepository.GetPhotosAsync(limit: 1000);
        var recent = allPhotos.Where(p => p.DateTaken >= thirtyDaysAgo || p.CreatedAt >= thirtyDaysAgo);

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Photos.Clear();
            foreach (var photo in recent)
                Photos.Add(new PhotoViewModel(photo));
            PhotoCountStatus = $"{Photos.Count} fotos (últimos 30 días)";
            StatusText = $"Filtrado: últimos 30 días – {Photos.Count} fotos";
        });
    }

    [RelayCommand]
    private async Task FilterByTagAsync(string tagName)
    {
        if (string.IsNullOrEmpty(tagName)) return;

        IsSemanticModeVisible = false;

        try
        {
            var photos = await _tagRepository.GetPhotosByTagAsync(tagName, limit: 500);
            var sorted = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Photos.Clear();
                foreach (var photo in sorted)
                    Photos.Add(new PhotoViewModel(photo));
                PhotoCountStatus = $"{Photos.Count} fotos con tag: {tagName}";
                StatusText = $"Mostrando {Photos.Count} fotos con tag: {tagName}";
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ReprocessTagsAsync()
    {
        StatusText = "Reprocesando tags...";
        IsProgressIndeterminate = true;

        try
        {
            var allPhotos = await _photoRepository.GetPhotosAsync(limit: 10000);
            var processed = 0;
            var total = allPhotos.Count();

            foreach (var photo in allPhotos)
            {
                await _autoTaggingService.UpdateTagsForExistingPhotoAsync(photo.Id, photo);
                processed++;

                if (processed % 50 == 0)
                {
                    StatusText = $"Reprocesando tags: {processed}/{total} fotos";
                    await Task.Delay(10);
                }
            }

            StatusText = $"Tags reprocesados: {processed} fotos";
            await LoadTagsAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error al reprocesar tags: {ex.Message}");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsProgressIndeterminate = false;
        }
    }
}
