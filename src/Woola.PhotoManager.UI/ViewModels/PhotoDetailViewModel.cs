using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.UI.ViewModels;

public partial class PhotoDetailViewModel : ObservableObject
{
    // ── Datos inmediatos (de PhotoViewModel) ─────────────────────────────────
    public int Id { get; }
    public string FileName { get; }
    public string ThumbnailPath { get; }
    public DateTime? DateTaken { get; }
    public string CameraModel { get; }
    public IRelayCommand CloseCommand { get; }

    // ── Cargados async desde repositorios ────────────────────────────────────
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileSize = "—";
    [ObservableProperty] private string _resolution = "—";
    [ObservableProperty] private string _lensModel = "—";
    [ObservableProperty] private string _aperture = "—";
    [ObservableProperty] private string _shutterSpeed = "—";
    [ObservableProperty] private string _iso = "—";
    [ObservableProperty] private string _focalLength = "—";
    [ObservableProperty] private string _indexedAt = string.Empty;
    [ObservableProperty] private string _faceSummary = string.Empty;

    // ── G1: Álbumes ───────────────────────────────────────────────────────────
    private AlbumRepository? _albumRepository;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddToAlbumCommand))]
    private Album? _selectedAlbum;

    [ObservableProperty] private string _albumAddStatus = string.Empty;
    public ObservableCollection<Album> AvailableAlbums { get; } = new();

    public ObservableCollection<Tag> Tags { get; } = new();

    // ─────────────────────────────────────────────────────────────────────────

    public PhotoDetailViewModel(PhotoViewModel vm, IRelayCommand closeCommand)
    {
        Id            = vm.Id;
        FileName      = vm.FileName;
        ThumbnailPath = vm.ThumbnailPath;
        DateTaken     = vm.DateTaken;
        CameraModel   = vm.CameraModel;
        CloseCommand  = closeCommand;
    }

    public async Task LoadDetailsAsync(
        PhotoRepository photoRepo,
        TagRepository tagRepo,
        FaceRepository faceRepo,
        AlbumRepository? albumRepo = null)
    {
        IsLoading        = true;
        _albumRepository = albumRepo;

        try
        {
            var photo = await photoRepo.GetPhotoByIdAsync(Id);
            if (photo != null)
            {
                FilePath     = photo.Path;
                FileSize     = FormatFileSize(photo.FileSize);
                Resolution   = photo.Width > 0 && photo.Height > 0
                               ? $"{photo.Width} × {photo.Height} px" : "—";
                LensModel    = string.IsNullOrEmpty(photo.LensModel) ? "—" : photo.LensModel;
                Aperture     = photo.Aperture.HasValue ? $"f/{photo.Aperture:F1}" : "—";
                ShutterSpeed = photo.ShutterSpeed.HasValue
                               ? FormatShutterSpeed(photo.ShutterSpeed.Value) : "—";
                Iso          = photo.Iso.HasValue ? $"ISO {photo.Iso}" : "—";
                FocalLength  = photo.FocalLength.HasValue ? $"{photo.FocalLength}mm" : "—";
                IndexedAt    = photo.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            }

            var tags  = await tagRepo.GetTagsForPhotoAsync(Id);
            var faces = await faceRepo.GetFacesForPhotoAsync(Id);

            // G1: cargar álbumes disponibles para el ComboBox
            if (albumRepo != null)
            {
                var albums = await albumRepo.GetAllAlbumsAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AvailableAlbums.Clear();
                    foreach (var a in albums) AvailableAlbums.Add(a);
                });
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Tags.Clear();
                foreach (var tag in tags.OrderByDescending(t => t.Confidence))
                    Tags.Add(tag);

                var faceList = faces.ToList();
                if (faceList.Count == 0) { FaceSummary = "Sin rostros detectados"; return; }

                var named   = faceList
                    .Where(f => f.IsUserConfirmed && !string.IsNullOrEmpty(f.PersonName))
                    .Select(f => f.PersonName!).ToList();
                var unknown = faceList.Count - named.Count;
                var parts   = new List<string>(named);
                if (unknown > 0) parts.Add($"{unknown} desconocido{(unknown > 1 ? "s" : "")}");

                FaceSummary = $"{faceList.Count} " +
                              $"{(faceList.Count == 1 ? "rostro" : "rostros")}: " +
                              $"{string.Join(", ", parts)}";
            });
        }
        finally { IsLoading = false; }
    }

    // ── G1: Agregar a álbum ───────────────────────────────────────────────────

    private bool CanAddToAlbum() => SelectedAlbum != null && _albumRepository != null;

    [RelayCommand(CanExecute = nameof(CanAddToAlbum))]
    private async Task AddToAlbumAsync()
    {
        if (_albumRepository == null || SelectedAlbum == null) return;
        try
        {
            await _albumRepository.AddPhotoToAlbumAsync(SelectedAlbum.Id, Id);
            AlbumAddStatus = $"✅ Añadida a '{SelectedAlbum.Name}'";
            await Task.Delay(3000);
            AlbumAddStatus = string.Empty;
        }
        catch (Exception ex) { AlbumAddStatus = $"Error: {ex.Message}"; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0) return "—";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    private static string FormatShutterSpeed(double ss)
        => ss >= 1 ? $"{ss:F1}s" : $"1/{Math.Round(1.0 / ss)}s";
}
