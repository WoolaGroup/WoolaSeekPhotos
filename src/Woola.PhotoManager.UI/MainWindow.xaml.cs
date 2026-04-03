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

        _photoIndexer = new PhotoIndexer(_photoRepository, tagRepository, thumbnailService);
        _photoIndexer.ProgressChanged += OnIndexingProgress;
    }

    private async void SelectFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderDialog dialog = new OpenFolderDialog();
        dialog.Title = "Seleccionar carpeta de fotos";

        if (dialog.ShowDialog() == true)
        {
            var folderPath = dialog.FolderName;
            FolderPathText.Text = folderPath;

            SelectFolderBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;

            _indexingCts = new CancellationTokenSource();

            try
            {
                await _photoIndexer!.StartIndexingAsync(folderPath, _indexingCts.Token);
                await LoadPhotosAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        var photos = await _photoRepository.GetPhotosAsync(limit: 200);

        Dispatcher.Invoke(() =>
        {
            _photos.Clear();
            foreach (var photo in photos)
            {
                _photos.Add(new PhotoViewModel(photo));
            }
            PhotoCountText.Text = $"{_photos.Count} fotos";
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
}

// ViewModel para la UI
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
}