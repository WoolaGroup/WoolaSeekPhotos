using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Database;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.UI;

public partial class PhotosByPersonWindow : Window
{
    public PhotosByPersonWindow(List<int> photoIds, string personName)
    {
        InitializeComponent();

        TitleText.Text = $"📸 {personName} - {photoIds.Count} fotos";

        LoadPhotos(photoIds);
    }

    private async void LoadPhotos(List<int> photoIds)
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Woola",
            "woola.db");

        var connectionFactory = new SqliteConnectionFactory(dbPath);
        var photoRepository = new PhotoRepository(connectionFactory);

        var photos = new ObservableCollection<Photo>();

        foreach (var id in photoIds)
        {
            var photo = await photoRepository.GetPhotoByIdAsync(id);
            if (photo != null)
                photos.Add(photo);
        }

        PhotosList.ItemsSource = photos;
    }
}