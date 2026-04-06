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
    private readonly IPhotoIndexer             _photoIndexer;
    private readonly IHybridSearchService      _hybridSearchService;
    private readonly PhotoRepository           _photoRepository;
    private readonly TagRepository             _tagRepository;
    private readonly IAutoTaggingService       _autoTaggingService;
    private readonly IFolderPickerService      _folderPickerService;
    private readonly FaceRepository            _faceRepository;
    private readonly AlbumRepository           _albumRepository;          // G1
    private readonly IEventDetectionService    _eventDetectionService;    // G3

    private CancellationTokenSource? _indexingCts;
    private readonly HashSet<string> _activeTagNames = new();

    // F2: Pagination state
    private const int PageSize = 50;
    private int _currentOffset = 0;
    private int _totalCount    = 0;

    // IMP-T3-003: Debounce + cancellation for search
    private CancellationTokenSource? _searchDebounce;
    private CancellationTokenSource? _searchCts;

    // IMP-T3-004: Filter pagination state
    private int    _filterOffset      = 0;
    private string _activeFilterType  = string.Empty;  // "album" | "tag" | ""
    private Album? _activeAlbum;

    // IMP-T3-002: RangeObservableCollection for batch UI updates
    public RangeObservableCollection<PhotoViewModel> Photos { get; } = new();
    public ObservableCollection<Tag>             Tags         { get; } = new();
    public ObservableCollection<FilterChip>      ActiveFilters{ get; } = new();
    public ObservableCollection<EventViewModel>  Events       { get; } = new();  // G3
    public ObservableCollection<Album>           SidebarAlbums{ get; } = new();  // G1

    public event EventHandler<string>? ErrorOccurred;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartIndexingCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopIndexingCommand))]
    private bool _isIndexing;

    [ObservableProperty] private string _searchText           = string.Empty;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _progressText         = "Listo";
    [ObservableProperty] private bool   _isProgressIndeterminate;
    [ObservableProperty] private string _statusText           = "Listo";
    [ObservableProperty] private string _photoCountText       = "0";
    [ObservableProperty] private string _photoCountStatus     = "0 fotos";
    [ObservableProperty] private string _folderPathText       = "Ninguna carpeta seleccionada";
    [ObservableProperty] private bool   _isHybridModeVisible;
    [ObservableProperty] private string _hybridModeLabel      = string.Empty;
    [ObservableProperty] private PhotoDetailViewModel? _selectedPhoto;
    [ObservableProperty] private bool   _isLoadingPhotos      = true;
    [ObservableProperty] private bool   _hasNoPhotos;
    [ObservableProperty] private bool   _hasMorePhotos;
    [ObservableProperty] private string _pageInfoText         = string.Empty;

    public MainViewModel(
        IPhotoIndexer          photoIndexer,
        IHybridSearchService   hybridSearchService,
        PhotoRepository        photoRepository,
        TagRepository          tagRepository,
        IAutoTaggingService    autoTaggingService,
        IFolderPickerService   folderPickerService,
        FaceRepository         faceRepository,
        AlbumRepository        albumRepository,
        IEventDetectionService eventDetectionService)
    {
        _photoIndexer          = photoIndexer;
        _hybridSearchService   = hybridSearchService;
        _photoRepository       = photoRepository;
        _tagRepository         = tagRepository;
        _autoTaggingService    = autoTaggingService;
        _folderPickerService   = folderPickerService;
        _faceRepository        = faceRepository;
        _albumRepository       = albumRepository;
        _eventDetectionService = eventDetectionService;

        _photoIndexer.ProgressChanged += OnIndexingProgress;
        Photos.CollectionChanged      += (_, _) => UpdatePhotoState();
        _ = InitializeAsync();
    }

    private void UpdatePhotoState() =>
        HasNoPhotos = Photos.Count == 0 && !IsLoadingPhotos;

    // IMP-T3-003: Debounce 300ms — cancela la query anterior al escribir
    partial void OnSearchTextChanged(string value)
    {
        if (IsHybridModeVisible) return;

        // Cancelar el temporizador previo
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        _searchDebounce = new CancellationTokenSource();

        // Cancelar la query DB anterior inmediatamente
        _searchCts?.Cancel();

        var debounceCts = _searchDebounce;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(300, debounceCts.Token); }
            catch (OperationCanceledException) { return; }  // nueva tecla llegó

            // Reiniciar token de búsqueda
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token  = _searchCts.Token;

            HasMorePhotos = false;
            if (string.IsNullOrEmpty(value))
                await LoadPhotosAsync(reset: true);
            else
                await FastSearchAsync(value, token);
        });
    }

    private async Task InitializeAsync()
    {
        await LoadPhotosAsync();
        await LoadTagsAsync();
        await LoadSidebarAlbumsAsync();   // G1
        await LoadEventsAsync();          // G3
    }

    // ── Pagination ────────────────────────────────────────────────────────────

    private async Task LoadPhotosAsync(bool reset = true)
    {
        IsLoadingPhotos = true;
        _activeFilterType = string.Empty;  // IMP-T3-004: salir de modo filtro
        try
        {
            if (reset)
            {
                _currentOffset = 0;
                _totalCount    = await _photoRepository.GetTotalCountAsync();
            }

            // IMP-T3-006B: la DB ya devuelve ORDER BY COALESCE(DateTaken,CreatedAt) DESC
            var photos = (await _photoRepository.GetPhotosAsync(limit: PageSize, offset: _currentOffset)).ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // IMP-T3-002: AddRange dispara 1 Reset en vez de N Add
                if (reset) Photos.ReplaceAll(photos.Select(p => new PhotoViewModel(p)));
                else       Photos.AddRange(photos.Select(p => new PhotoViewModel(p)));

                _currentOffset += photos.Count;

                var isFiltered   = _activeTagNames.Count > 0 || IsHybridModeVisible;
                HasMorePhotos    = !isFiltered && photos.Count >= PageSize;
                PhotoCountText   = _totalCount.ToString();
                PhotoCountStatus = isFiltered ? $"{Photos.Count} resultados" : $"{Photos.Count} fotos";
                PageInfoText     = _totalCount > 0 && !isFiltered
                    ? $"Mostrando {Photos.Count} de {_totalCount}" : string.Empty;

                StatusText = reset
                    ? (isFiltered ? $"{Photos.Count} fotos filtradas" : $"{Photos.Count} de {_totalCount} fotos")
                    : $"Cargadas {Photos.Count} de {_totalCount} fotos";
            });
        }
        finally
        {
            IsLoadingPhotos = false;
            UpdatePhotoState();
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMorePhotos || IsLoadingPhotos) return;
        // IMP-T3-004: enrutar al contexto activo de filtro
        switch (_activeFilterType)
        {
            case "album" when _activeAlbum != null:
                await LoadAlbumPageAsync(_activeAlbum, reset: false);
                break;
            case "tag" when _activeTagNames.Count > 0:
                await LoadTagPageAsync(reset: false);
                break;
            default:
                await LoadPhotosAsync(reset: false);
                break;
        }
    }

    // ── Tags ──────────────────────────────────────────────────────────────────

    private async Task LoadTagsAsync()
    {
        var tags = await _tagRepository.GetAllTagsAsync();
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Tags.Clear();
            foreach (var tag in tags.Take(20)) Tags.Add(tag);
        });
    }

    // ── G1: Albums sidebar ────────────────────────────────────────────────────

    private async Task LoadSidebarAlbumsAsync()
    {
        try
        {
            var albums = await _albumRepository.GetAllAlbumsAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SidebarAlbums.Clear();
                foreach (var a in albums) SidebarAlbums.Add(a);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Albums] Error cargando: {ex.Message}");
        }
    }

    /// <summary>Llama MainWindow después de cerrar AlbumWindow para refrescar.</summary>
    public async Task RefreshAlbumsAsync() => await LoadSidebarAlbumsAsync();

    // IMP-T3-004: Paginación en filtro de álbum
    [RelayCommand]
    private async Task FilterByAlbumAsync(Album album)
    {
        IsHybridModeVisible = false;
        _activeTagNames.Clear();
        _activeFilterType   = "album";
        _activeAlbum        = album;
        _filterOffset       = 0;
        await LoadAlbumPageAsync(album, reset: true);
    }

    private async Task LoadAlbumPageAsync(Album album, bool reset)
    {
        IsLoadingPhotos = true;
        try
        {
            var (photos, total) = await _albumRepository.GetPhotosInAlbumPagedAsync(
                album.Id, limit: PageSize, offset: _filterOffset);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var vms = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt)
                                .Select(p => new PhotoViewModel(p));

                // IMP-T3-002: batch update
                if (reset) Photos.ReplaceAll(vms);
                else        Photos.AddRange(vms);

                _filterOffset   += photos.Count;
                HasMorePhotos    = photos.Count >= PageSize;
                PhotoCountStatus = $"{Photos.Count} fotos";
                PageInfoText     = total > 0 ? $"Mostrando {Photos.Count} de {total}" : string.Empty;
                StatusText       = reset
                    ? $"📁 {album.Name} – {Photos.Count} de {total} fotos"
                    : $"📁 {album.Name} – cargadas {Photos.Count} de {total} fotos";

                if (reset)
                {
                    ActiveFilters.Clear();
                    ActiveFilters.Add(new FilterChip($"📁 {album.Name}", () => _ = FilterAllAsync()));
                }
            });
        }
        finally
        {
            IsLoadingPhotos = false;
            UpdatePhotoState();
        }
    }

    // ── G3: Events sidebar ────────────────────────────────────────────────────

    private async Task LoadEventsAsync()
    {
        try
        {
            var eventList = await _eventDetectionService.DetectEventsAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Events.Clear();
                foreach (var ev in eventList) Events.Add(new EventViewModel(ev));
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Events] Error detectando: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task FilterByEventAsync(EventViewModel ev)
    {
        IsHybridModeVisible = false;
        _activeTagNames.Clear();

        var photos = await _photoRepository.GetPhotosByDateRangeAsync(ev.Event.Start, ev.Event.End);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // IMP-T3-002: ReplaceAll
            var sorted = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt)
                               .Select(p => new PhotoViewModel(p));
            Photos.ReplaceAll(sorted);

            HasMorePhotos    = false;
            PhotoCountStatus = $"{Photos.Count} fotos";
            StatusText       = $"📅 {ev.Event.Name} – {Photos.Count} fotos";
            PageInfoText     = string.Empty;

            ActiveFilters.Clear();
            ActiveFilters.Add(new FilterChip($"📅 {ev.Event.Name}", () => _ = FilterAllAsync()));
        });
    }

    // ── Search ────────────────────────────────────────────────────────────────

    // IMP-T3-003: CancellationToken para poder cancelar si otra tecla llega
    private async Task FastSearchAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var photos = await _photoRepository.SearchCandidatesAsync(query, limit: 200, cancellationToken: ct);
            ct.ThrowIfCancellationRequested();

            // IMP-T3-002: ReplaceAll — un único Reset en vez de N Add
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var sorted = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt)
                                   .Select(p => new PhotoViewModel(p));
                Photos.ReplaceAll(sorted);
                PhotoCountStatus = $"{Photos.Count} resultados";
            });
        }
        catch (OperationCanceledException) { /* búsqueda supersedida — no actualizar UI */ }
    }

    private void OnIndexingProgress(object? sender, IndexingProgress e)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressValue = e.Percentage;
            ProgressText  = $"{e.Processed}/{e.TotalFound} – {e.CurrentFile}";
            StatusText    = e.Processed < e.TotalFound ? "Indexando..." : "Indexación completa";
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
                // IMP-T3-002: ReplaceAll — 1 Reset en lugar de N Add
                Photos.ReplaceAll(results.Select(r => new PhotoViewModel(r.Photo)));

                var exactCount      = results.Count(r => r.ExactMatches > 0);
                IsHybridModeVisible = true;
                HybridModeLabel     = $"Búsqueda híbrida: '{query}' – {results.Count} resultados ({exactCount} exactos)";
                PhotoCountStatus    = $"{results.Count} resultados híbridos";
                StatusText          = $"🔍 Híbrida: {results.Count} resultados";
            });
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error en búsqueda: {ex.Message}");
            StatusText = $"Error: {ex.Message}";
        }
        finally { IsProgressIndeterminate = false; }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        IsHybridModeVisible = false;
        _activeFilterType   = string.Empty;  // IMP-T3-004
        _activeAlbum        = null;
        SearchText          = string.Empty;
        StatusText          = "Búsqueda cancelada";
        ActiveFilters.Clear();
        _activeTagNames.Clear();
        _searchCts?.Cancel();  // IMP-T3-003: cancelar query en vuelo
        _ = LoadPhotosAsync(reset: true);
    }

    [RelayCommand]
    private async Task FilterAllAsync()
    {
        IsHybridModeVisible = false;
        _activeTagNames.Clear();
        ActiveFilters.Clear();
        _activeFilterType = string.Empty;  // IMP-T3-004
        _activeAlbum      = null;
        await LoadPhotosAsync(reset: true);
    }

    [RelayCommand]
    private async Task FilterRecentAsync()
    {
        IsHybridModeVisible = false;
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var allPhotos     = await _photoRepository.GetPhotosAsync(limit: 1000);
        var recent        = allPhotos.Where(p => p.DateTaken >= thirtyDaysAgo || p.CreatedAt >= thirtyDaysAgo);

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // IMP-T3-002: ReplaceAll
            Photos.ReplaceAll(recent.Select(p => new PhotoViewModel(p)));
            PhotoCountStatus = $"{Photos.Count} fotos (últimos 30 días)";
            StatusText       = $"Filtrado: últimos 30 días – {Photos.Count} fotos";

            ActiveFilters.Clear();
            ActiveFilters.Add(new FilterChip("🕐 Últimos 30 días", () =>
            {
                ActiveFilters.Clear();
                _ = FilterAllAsync();
            }));
        });
    }

    // IMP-T3-004: Paginación en filtro de tag
    [RelayCommand]
    private async Task FilterByTagAsync(string tagName)
    {
        if (string.IsNullOrEmpty(tagName)) return;
        IsHybridModeVisible = false;

        if (_activeTagNames.Contains(tagName)) _activeTagNames.Remove(tagName);
        else _activeTagNames.Add(tagName);

        if (_activeTagNames.Count == 0) { await FilterAllAsync(); return; }

        _activeFilterType = "tag";
        _filterOffset     = 0;
        await LoadTagPageAsync(reset: true);
    }

    private async Task LoadTagPageAsync(bool reset)
    {
        IsLoadingPhotos = true;
        try
        {
            var (photos, total) = await _tagRepository.GetPhotosByTagsPagedAsync(
                _activeTagNames, limit: PageSize, offset: _filterOffset);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var vms = photos.OrderByDescending(p => p.DateTaken ?? p.CreatedAt)
                                .Select(p => new PhotoViewModel(p));

                // IMP-T3-002: batch update
                if (reset) Photos.ReplaceAll(vms);
                else        Photos.AddRange(vms);

                _filterOffset   += photos.Count;
                HasMorePhotos    = photos.Count >= PageSize;
                var tagList      = string.Join(" + ", _activeTagNames);
                PhotoCountStatus = $"{Photos.Count} fotos";
                PageInfoText     = total > 0 ? $"Mostrando {Photos.Count} de {total}" : string.Empty;
                StatusText       = $"Filtro AND: {tagList} – {Photos.Count} de {total} fotos";

                if (reset)
                {
                    ActiveFilters.Clear();
                    foreach (var tag in _activeTagNames.ToList())
                    {
                        var t = tag;
                        ActiveFilters.Add(new FilterChip($"🏷️ {t}", () => _ = FilterByTagAsync(t)));
                    }
                }
            });
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally
        {
            IsLoadingPhotos = false;
            UpdatePhotoState();
        }
    }

    // ── Photo detail ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SelectPhotoAsync(PhotoViewModel vm)
    {
        var detail = new PhotoDetailViewModel(vm, CloseDetailCommand);
        SelectedPhoto = detail;
        await detail.LoadDetailsAsync(_photoRepository, _tagRepository, _faceRepository, _albumRepository);
    }

    [RelayCommand]
    private void CloseDetail() => SelectedPhoto = null;

    // ── Indexing ──────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanStartIndexing))]
    private async Task StartIndexingAsync()
    {
        var folderPath = _folderPickerService.PickFolder("Seleccionar carpeta de fotos");
        if (folderPath == null) return;

        FolderPathText = folderPath;
        IsIndexing     = true;
        _indexingCts   = new CancellationTokenSource();

        try
        {
            await _photoIndexer.StartIndexingAsync(folderPath, _indexingCts.Token);
            _hybridSearchService.InvalidateCache();   // F4
            await LoadPhotosAsync(reset: true);
            await LoadEventsAsync();                  // G3: refresh after new photos
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error durante la indexación: {ex.Message}");
        }
        finally { IsIndexing = false; }
    }
    private bool CanStartIndexing() => !IsIndexing;

    [RelayCommand(CanExecute = nameof(IsIndexing))]
    private void StopIndexing()
    {
        _indexingCts?.Cancel();
        StatusText = "Deteniendo...";
    }
    private bool IsIndexingCanExecute() => IsIndexing;

    // IMP-T3-003: Liberar CancellationTokenSources al cerrar
    public void Dispose()
    {
        _searchDebounce?.Dispose();
        _searchCts?.Dispose();
        _indexingCts?.Dispose();
    }

    [RelayCommand]
    private async Task ReprocessTagsAsync()
    {
        StatusText              = "Reprocesando tags...";
        IsProgressIndeterminate = true;

        try
        {
            var allPhotos = (await _photoRepository.GetPhotosAsync(limit: 10000)).ToList();
            var total     = allPhotos.Count;
            var processed = 0;

            // F1: Parallelizar con 4 workers concurrentes
            await Parallel.ForEachAsync(allPhotos,
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (photo, _) =>
                {
                    await _autoTaggingService.UpdateTagsForExistingPhotoAsync(photo.Id, photo);
                    var p = System.Threading.Interlocked.Increment(ref processed);
                    if (p % 50 == 0)
                        System.Windows.Application.Current.Dispatcher.Invoke(
                            () => StatusText = $"Reprocesando tags: {p}/{total}");
                });

            StatusText = $"Tags reprocesados: {processed} fotos";
            await LoadTagsAsync();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, $"Error al reprocesar tags: {ex.Message}");
            StatusText = $"Error: {ex.Message}";
        }
        finally { IsProgressIndeterminate = false; }
    }
}
