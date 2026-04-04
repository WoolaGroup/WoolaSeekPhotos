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
    private readonly IHybridSearchService _hybridSearchService;
    private readonly PhotoRepository _photoRepository;
    private readonly TagRepository _tagRepository;
    private readonly IAutoTaggingService _autoTaggingService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly FaceRepository _faceRepository;

    private CancellationTokenSource? _indexingCts;
    private readonly HashSet<string> _activeTagNames = new();

    public ObservableCollection<PhotoViewModel> Photos { get; } = new();
    public ObservableCollection<Tag> Tags { get; } = new();
    public ObservableCollection<FilterChip> ActiveFilters { get; } = new();

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
    [ObservableProperty] private bool _isHybridModeVisible;
    [ObservableProperty] private string _hybridModeLabel = string.Empty;
    [ObservableProperty] private PhotoDetailViewModel? _selectedPhoto;
    [ObservableProperty] private bool _isLoadingPhotos = true;
    [ObservableProperty] private bool _hasNoPhotos;

    public MainViewModel(
        IPhotoIndexer photoIndexer,
        IHybridSearchService hybridSearchService,
        PhotoRepository photoRepository,
        TagRepository tagRepository,
        IAutoTaggingService autoTaggingService,
        IFolderPickerService folderPickerService,
        FaceRepository faceRepository)
    {
        _photoIndexer = photoIndexer;
        _hybridSearchService = hybridSearchService;
        _photoRepository = photoRepository;
        _tagRepository = tagRepository;
        _autoTaggingService = autoTaggingService;
        _folderPickerService = folderPickerService;
        _faceRepository = faceRepository;

        _photoIndexer.ProgressChanged += OnIndexingProgress;
        Photos.CollectionChanged += (_, _) => UpdatePhotoState();
        _ = InitializeAsync();
    }

    private void UpdatePhotoState() =>
        HasNoPhotos = Photos.Count == 0 && !IsLoadingPhotos;

    partial void OnSearchTextChanged(string value)
    {
        if (IsHybridModeVisible) return;
        _ = string.IsNullOrEmpty(value) ? LoadPhotosAsync() : FastSearchAsync(value);
    }

    private async Task InitializeAsync()
    {
        await LoadPhotosAsync();
        await LoadTagsAsync();
    }

    private async Task LoadPhotosAsync()
    {
        IsLoadingPhotos = true;
        try
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
        finally
        {
            IsLoadingPhotos = false;
            UpdatePhotoState();
        }
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

    private async Task FastSearchAsync(string query)
    {
        var photos = await _photoRepository.SearchCandidatesAsync(query, limit: 200);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Photos.Clear();
            foreach (var photo in photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt))
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

    [RelayCommand]
    private async Task HybridSearchAsync()
    {
        var query = SearchText.Trim();
        if (string.IsNullOrEmpty(query)) { StatusText = "Escribe algo para buscar..."; return; }

        StatusText = $"🔍 Buscando: '{query}'...";
        IsProgressIndeterminate = true;

        try
        {
            var results = await _hybridSearchService.SearchAsync(query, limit: 200);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Photos.Clear();
                foreach (var r in results)
                    Photos.Add(new PhotoViewModel(r.Photo));

                var exactCount = results.Count(r => r.ExactMatches > 0);
                IsHybridModeVisible = true;
                HybridModeLabel = $"Búsqueda híbrida: '{query}' – {results.Count} resultados ({exactCount} exactos)";
                PhotoCountStatus = $"{results.Count} resultados híbridos";
                StatusText = $"🔍 Híbrida: {results.Count} resultados";
            });
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error en búsqueda: {ex.Message}");
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
        IsHybridModeVisible = false;
        SearchText = string.Empty;
        StatusText = "Búsqueda cancelada";
        ActiveFilters.Clear();
        _activeTagNames.Clear();
        _ = LoadPhotosAsync();
    }

    [RelayCommand]
    private async Task FilterAllAsync()
    {
        IsHybridModeVisible = false;
        _activeTagNames.Clear();
        ActiveFilters.Clear();
        await LoadPhotosAsync();
        StatusText = $"Mostrando todas las fotos ({Photos.Count})";
    }

    [RelayCommand]
    private async Task FilterRecentAsync()
    {
        IsHybridModeVisible = false;
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

            ActiveFilters.Clear();
            ActiveFilters.Add(new FilterChip("🕐 Últimos 30 días", () =>
            {
                ActiveFilters.Clear();
                _ = FilterAllAsync();
            }));
        });
    }

    [RelayCommand]
    private async Task FilterByTagAsync(string tagName)
    {
        if (string.IsNullOrEmpty(tagName)) return;
        IsHybridModeVisible = false;

        if (_activeTagNames.Contains(tagName))
            _activeTagNames.Remove(tagName);
        else
            _activeTagNames.Add(tagName);

        if (_activeTagNames.Count == 0) { await FilterAllAsync(); return; }

        try
        {
            var photos = await _tagRepository.GetPhotosByTagsAsync(_activeTagNames);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Photos.Clear();
                foreach (var photo in photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt))
                    Photos.Add(new PhotoViewModel(photo));

                PhotoCountStatus = $"{Photos.Count} fotos";
                StatusText = $"Filtro AND: {string.Join(" + ", _activeTagNames)} – {Photos.Count} fotos";

                ActiveFilters.Clear();
                foreach (var tag in _activeTagNames.ToList())
                {
                    var capturedTag = tag;
                    ActiveFilters.Add(new FilterChip($"🏷️ {capturedTag}",
                        () => _ = FilterByTagAsync(capturedTag)));
                }
            });
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SelectPhotoAsync(PhotoViewModel vm)
    {
        var detail = new PhotoDetailViewModel(vm, CloseDetailCommand);
        SelectedPhoto = detail;
        await detail.LoadDetailsAsync(_photoRepository, _tagRepository, _faceRepository);
    }

    [RelayCommand]
    private void CloseDetail() => SelectedPhoto = null;

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
