using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;
using Brushes    = System.Windows.Media.Brushes;
using Button     = System.Windows.Controls.Button;
using Cursors    = System.Windows.Input.Cursors;
using Image      = System.Windows.Controls.Image;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

public partial class AlbumWindow : Window
{
    private readonly AlbumRepository  _albumRepository;
    private readonly PhotoRepository  _photoRepository;
    private readonly TagRepository    _tagRepository;
    private readonly ExportService    _exportService;
    private int    _selectedAlbumId   = -1;
    private string _selectedAlbumName = string.Empty;

    public AlbumWindow(AlbumRepository albumRepository, PhotoRepository photoRepository,
                       TagRepository tagRepository)
    {
        _albumRepository = albumRepository;
        _photoRepository = photoRepository;
        _tagRepository   = tagRepository;
        _exportService   = new ExportService(_albumRepository, _photoRepository, _tagRepository);
        InitializeComponent();
        _ = LoadAlbumsAsync();
        _ = LoadRecentPhotosAsync();
    }

    // ── Cargar álbumes ────────────────────────────────────────────────────────

    private async Task LoadAlbumsAsync()
    {
        var albums = await _albumRepository.GetAllAlbumsAsync();
        Dispatcher.Invoke(() =>
        {
            AlbumListBox.ItemsSource = null;
            AlbumListBox.ItemsSource = albums;
            StatusTxt.Text = $"{albums.Count} álbumes";
        });
    }

    // ── Selección de álbum ────────────────────────────────────────────────────

    private void AlbumListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AlbumListBox.SelectedItem is not Album album) return;
        _selectedAlbumId   = album.Id;
        _selectedAlbumName = album.Name;
        AlbumNameTxt.Text  = album.Name;
        AlbumCountTxt.Text = $"{album.PhotoCount} foto{(album.PhotoCount != 1 ? "s" : "")}";
        _ = LoadAlbumPhotosAsync(album.Id);
        _ = RefreshAllPhotosThumbs();
    }

    // ── Fotos del álbum ───────────────────────────────────────────────────────

    private async Task LoadAlbumPhotosAsync(int albumId)
    {
        var photos = (await _albumRepository.GetPhotosInAlbumAsync(albumId)).ToList();
        Dispatcher.Invoke(() =>
        {
            AlbumPhotosPanel.Children.Clear();
            foreach (var photo in photos)
                AlbumPhotosPanel.Children.Add(CreatePhotoThumb(photo, isInAlbum: true));

            AlbumCountTxt.Text = $"{photos.Count} foto{(photos.Count != 1 ? "s" : "")}";
        });
    }

    // ── Fotos recientes (panel inferior) ──────────────────────────────────────

    private async Task LoadRecentPhotosAsync()
    {
        var photos = (await _photoRepository.GetPhotosAsync(limit: 80)).ToList();
        Dispatcher.Invoke(() =>
        {
            AllPhotosPanel.Children.Clear();
            foreach (var photo in photos)
                AllPhotosPanel.Children.Add(CreatePhotoThumb(photo, isInAlbum: false));
        });
    }

    private async Task RefreshAllPhotosThumbs()
    {
        if (_selectedAlbumId == -1) return;
        var albumIds = await _albumRepository.GetPhotoIdsInAlbumAsync(_selectedAlbumId);
        Dispatcher.Invoke(() =>
        {
            foreach (UIElement el in AllPhotosPanel.Children)
            {
                if (el is Border b && b.Tag is int pid)
                {
                    b.BorderBrush     = albumIds.Contains(pid) ? Brushes.ForestGreen : Brushes.Transparent;
                    b.BorderThickness = albumIds.Contains(pid) ? new Thickness(2) : new Thickness(0);
                }
            }
        });
    }

    // ── Crear thumbnail ───────────────────────────────────────────────────────

    private Border CreatePhotoThumb(Photo photo, bool isInAlbum)
    {
        var img = new Image
        {
            Width  = 80,
            Height = 80,
            Stretch = Stretch.UniformToFill
        };

        if (!string.IsNullOrEmpty(photo.ThumbnailPath) && File.Exists(photo.ThumbnailPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource         = new Uri(photo.ThumbnailPath);
                bmp.CacheOption       = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth  = 80;
                bmp.EndInit();
                img.Source = bmp;
            }
            catch { /* thumbnail inaccesible */ }
        }

        var border = new Border
        {
            Width           = 84,
            Height          = 84,
            Margin          = new Thickness(4),
            CornerRadius    = new CornerRadius(5),
            ClipToBounds    = true,
            Cursor          = Cursors.Hand,
            Tag             = photo.Id,
            ToolTip         = photo.FileName,
            BorderThickness = isInAlbum ? new Thickness(2) : new Thickness(0),
            BorderBrush     = isInAlbum ? Brushes.DodgerBlue : Brushes.Transparent,
            Child           = img
        };

        border.MouseLeftButtonUp += isInAlbum ? RemovePhotoFromAlbum_Click : AddPhotoToAlbum_Click;
        return border;
    }

    // ── Agregar / quitar fotos ────────────────────────────────────────────────

    private async void AddPhotoToAlbum_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedAlbumId == -1)
        {
            MessageBox.Show("Selecciona un álbum primero.", "Sin álbum", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (sender is not Border { Tag: int photoId }) return;

        await _albumRepository.AddPhotoToAlbumAsync(_selectedAlbumId, photoId);
        StatusTxt.Text = "Foto añadida al álbum.";
        await LoadAlbumPhotosAsync(_selectedAlbumId);
        await RefreshAllPhotosThumbs();
        await LoadAlbumsAsync(); // actualizar conteo en sidebar
    }

    private async void RemovePhotoFromAlbum_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: int photoId }) return;
        if (_selectedAlbumId == -1) return;

        if (MessageBox.Show("¿Quitar esta foto del álbum?", "Confirmar",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        await _albumRepository.RemovePhotoFromAlbumAsync(_selectedAlbumId, photoId);
        StatusTxt.Text = "Foto eliminada del álbum.";
        await LoadAlbumPhotosAsync(_selectedAlbumId);
        await LoadAlbumsAsync();
    }

    // ── Crear álbum ───────────────────────────────────────────────────────────

    private async void CreateAlbumBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CreateAlbumDialog { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.AlbumName)) return;

        await _albumRepository.CreateAlbumAsync(dialog.AlbumName.Trim(), dialog.AlbumDescription?.Trim());
        StatusTxt.Text = $"Álbum '{dialog.AlbumName}' creado.";
        await LoadAlbumsAsync();
    }

    // ── Exportar álbum como ZIP ───────────────────────────────────────────────

    private async void ExportZipBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedAlbumId == -1)
        {
            MessageBox.Show("Selecciona un álbum primero.", "Sin álbum",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title      = "Guardar álbum como ZIP",
            Filter     = "Archivo ZIP|*.zip",
            FileName   = $"{_selectedAlbumName}_{DateTime.Now:yyyy-MM-dd}.zip",
            DefaultExt = ".zip"
        };

        if (saveDialog.ShowDialog() != true) return;

        ExportZipBtn.IsEnabled = false;
        StatusTxt.Text = "Exportando...";

        try
        {
            var progress = new Progress<string>(msg =>
                Dispatcher.Invoke(() => StatusTxt.Text = msg));

            var zipPath = await _exportService.ExportAlbumToZipAsync(
                _selectedAlbumId, _selectedAlbumName, saveDialog.FileName, progress);

            StatusTxt.Text = $"✓ ZIP creado: {System.IO.Path.GetFileName(zipPath)}";
            MessageBox.Show($"Álbum exportado correctamente:\n{zipPath}",
                            "Exportación completa", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusTxt.Text = $"Error al exportar: {ex.Message}";
            MessageBox.Show($"Error al exportar: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ExportZipBtn.IsEnabled = true;
        }
    }

    // ── Eliminar álbum ────────────────────────────────────────────────────────

    private async void DeleteAlbumBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int albumId }) return;
        var album = (AlbumListBox.ItemsSource as List<Album>)?.FirstOrDefault(a => a.Id == albumId);
        if (album == null) return;

        if (MessageBox.Show($"¿Eliminar el álbum '{album.Name}'?\n(Las fotos no se borran.)",
                "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        await _albumRepository.DeleteAlbumAsync(albumId);
        _selectedAlbumId = -1;
        AlbumNameTxt.Text  = "Selecciona un álbum";
        AlbumCountTxt.Text = string.Empty;
        AlbumPhotosPanel.Children.Clear();
        StatusTxt.Text = "Álbum eliminado.";
        await LoadAlbumsAsync();
    }
}
