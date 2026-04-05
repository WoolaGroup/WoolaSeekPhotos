using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Core.Agents.Agents;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Database;
using Woola.PhotoManager.Infrastructure.Repositories;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

public partial class FaceManagementWindow : Window
{
    private readonly FaceRepository _faceRepository;
    private readonly PhotoRepository _photoRepository;
    private readonly TagRepository _tagRepository;
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly FaceClusteringService _clusteringService;
    private ObservableCollection<PersonGroup> _personGroups = new();

    public FaceManagementWindow()
    {
        InitializeComponent();

        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Woola",
            "woola.db");

        _connectionFactory = new SqliteConnectionFactory(dbPath);
        _faceRepository    = new FaceRepository(_connectionFactory);
        _photoRepository   = new PhotoRepository(_connectionFactory);
        _tagRepository     = new TagRepository(_connectionFactory);
        _clusteringService = new FaceClusteringService(
            _faceRepository,
            NullLogger<FaceClusteringService>.Instance);

        PersonGroupsList.ItemsSource = _personGroups;

        // Cargar grupos existentes
        LoadGroups();

        // Verificar si hay rostros, si no, sugerir reprocesar
        CheckAndSuggestReprocess();
    }

    private async void CheckAndSuggestReprocess()
    {
        var allFaces = await _faceRepository.GetAllFacesAsync();

        if (!allFaces.Any())
        {
            var statusText = FindName("StatusText") as System.Windows.Controls.TextBlock;
            if (statusText != null)
            {
                statusText.Text = "⚠️ No hay rostros detectados. Haz clic en 'Reprocesar Rostros' para detectarlos.";
            }
        }
    }


    private async void LoadGroups()
    {
        var statusText = FindName("StatusText") as System.Windows.Controls.TextBlock;
        if (statusText != null) statusText.Text = "Cargando rostros...";

        try
        {
            var allFaces = (await _faceRepository.GetAllFacesAsync()).ToList();
            _personGroups.Clear();

            // Group faces by PersonId; faces without PersonId are solo groups
            var grouped = allFaces
                .GroupBy(f => f.PersonId ?? $"_solo_{f.Id}")
                .OrderByDescending(g => g.Count());

            foreach (var g in grouped)
            {
                var faceList = g.ToList();
                var displayName = faceList.FirstOrDefault(f => !string.IsNullOrEmpty(f.PersonName))?.PersonName
                                  ?? "👤 Persona sin identificar";

                var personGroup = new PersonGroup
                {
                    Id = g.Key,
                    Faces = faceList,
                    DisplayName = displayName,
                    FaceThumbnails = await GetFaceThumbnails(faceList)
                };
                _personGroups.Add(personGroup);
            }

            if (statusText != null)
                statusText.Text = $"{_personGroups.Count} personas, {allFaces.Count} rostros";
        }
        catch (Exception ex)
        {
            var statusText2 = FindName("StatusText") as System.Windows.Controls.TextBlock;
            if (statusText2 != null) statusText2.Text = $"Error: {ex.Message}";
        }
    }

    private async Task<List<string>> GetFaceThumbnails(List<Face> faces)
    {
        var thumbnails = new List<string>();

        foreach (var face in faces.Take(9))
        {
            var photo = await _photoRepository.GetPhotoByIdAsync(face.PhotoId);
            if (photo != null && File.Exists(photo.Path))
            {
                thumbnails.Add(photo.Path);
            }
        }

        return thumbnails;
    }



    private async void AssignName_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as System.Windows.Controls.Button;
        var group = button?.Tag as PersonGroup;

        if (group == null || group.Faces.Count == 0) return;

        var firstFace = group.Faces.First();
        var dialog = new InputDialog("Asignar nombre a la persona", "Nombre:",
                                      firstFace.PersonName ?? "");

        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.Answer))
        {
            var newName = dialog.Answer.Trim();
            // Reuse existing PersonId if any face in the group already has one
            var personId = group.Faces.FirstOrDefault(f => !string.IsNullOrEmpty(f.PersonId))?.PersonId
                           ?? Guid.NewGuid().ToString();

            var tagName = $"persona_{newName.ToLower().Replace(" ", "_")}";
            var tagId = await _tagRepository.GetOrCreateTagAsync(tagName, "Person", true);

            // Update ALL faces in the group
            foreach (var face in group.Faces)
            {
                await _faceRepository.UpdatePersonNameAsync(face.Id, newName, personId);
                await _tagRepository.AddTagToPhotoAsync(face.PhotoId, tagId, face.Confidence, "FaceAgent");
            }

            LoadGroups();

            var statusText = FindName("StatusText") as System.Windows.Controls.TextBlock;
            if (statusText != null)
                statusText.Text = $"'{newName}' asignado a {group.Faces.Count} rostros";
        }
    }



    private async void TestSingleImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Seleccionar imagen con rostros",
            Filter = "Imagenes|*.jpg;*.jpeg;*.png;*.bmp"
        };

        if (dialog.ShowDialog() == true)
        {
            var faceService = new FaceService();
            var faces = await faceService.DetectFacesAsync(dialog.FileName);

            var statusText = FindName("StatusText") as System.Windows.Controls.TextBlock;
            if (statusText != null)
            {
                statusText.Text = $"Rostros detectados: {faces.Count}";
            }

            MessageBox.Show($"Se detectaron {faces.Count} rostros en la imagen",
                            "Resultado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ViewPhotos_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as System.Windows.Controls.Button;
        var group = button?.Tag as PersonGroup;

        if (group == null) return;

        var photoIds = group.Faces.Select(f => f.PhotoId).Distinct().ToList();
        var photosWindow = new PhotosByPersonWindow(photoIds, group.DisplayName);
        photosWindow.Owner = this;
        photosWindow.ShowDialog();
    }



    private async void ReprocessFaces_Click(object sender, RoutedEventArgs e)
    {
        var statusText = FindName("StatusText") as System.Windows.Controls.TextBlock;
        var reprocessBtn = sender as System.Windows.Controls.Button;

        if (statusText != null) statusText.Text = "Reprocesando rostros...";
        if (reprocessBtn != null) reprocessBtn.IsEnabled = false;

        try
        {
            await _faceRepository.DeleteAllFacesAsync();

            var allPhotos = await _photoRepository.GetPhotosAsync(limit: 10000);
            var total = allPhotos.Count();
            var processed = 0;
            var totalFaces = 0;

            using var faceService = new FaceService();

            foreach (var photo in allPhotos)
            {
                if (!System.IO.File.Exists(photo.Path)) { processed++; continue; }

                var detectedFaces = await faceService.DetectFacesAsync(photo.Path);

                foreach (var detectedFace in detectedFaces)
                {
                    var embedding = await faceService.GenerateEmbeddingAsync(photo.Path, detectedFace);
                    var hasEmbedding = embedding.Any(v => v != 0f);

                    var face = new Face
                    {
                        PhotoId = photo.Id,
                        PersonName = null,
                        PersonId = null,
                        X = detectedFace.X,
                        Y = detectedFace.Y,
                        Width = detectedFace.Width,
                        Height = detectedFace.Height,
                        Encoding = hasEmbedding ? SerializeEmbedding(embedding) : null,
                        Confidence = detectedFace.Confidence,
                        IsUserConfirmed = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _faceRepository.InsertFaceAsync(face);
                    totalFaces++;
                }

                processed++;
                if (processed % 10 == 0 && statusText != null)
                {
                    statusText.Text = $"Procesando: {processed}/{total} fotos – {totalFaces} rostros";
                    await Task.Delay(1);
                }
            }

            if (statusText != null)
                statusText.Text = $"Completado: {totalFaces} rostros en {total} fotos";
            LoadGroups();
        }
        catch (Exception ex)
        {
            var statusText2 = FindName("StatusText") as System.Windows.Controls.TextBlock;
            if (statusText2 != null) statusText2.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (reprocessBtn != null) reprocessBtn.IsEnabled = true;
        }
    }

    private async void ClusterAndSave_Click(object sender, RoutedEventArgs e)
    {
        var statusText = FindName("StatusText") as System.Windows.Controls.TextBlock;
        var clusterBtn = sender as System.Windows.Controls.Button;

        if (statusText != null) statusText.Text = "Agrupando rostros por similitud...";
        if (clusterBtn != null) clusterBtn.IsEnabled = false;

        try
        {
            // IMP-002: delega al FaceClusteringService (centroide dinámico, average-linkage)
            var result = await _clusteringService.ClusterAsync(threshold: 0.65f);

            if (result.TotalFaces == 0)
            {
                MessageBox.Show("No hay rostros con embeddings. Primero usa 'Reprocesar Rostros'.",
                                "Sin datos", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (statusText != null)
                statusText.Text = $"Agrupados: {result.ClusterCount} personas de {result.TotalFaces} rostros ({result.UpdatedFaces} actualizados)";

            LoadGroups();
        }
        catch (Exception ex)
        {
            var statusText2 = FindName("StatusText") as System.Windows.Controls.TextBlock;
            if (statusText2 != null) statusText2.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Error al agrupar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (clusterBtn != null) clusterBtn.IsEnabled = true;
        }
    }

    private byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * 4];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

}

public class PersonGroup
{
    public string Id { get; set; } = string.Empty;
    public List<Face> Faces { get; set; } = new();
    public string DisplayName { get; set; } = string.Empty;
    public List<string> FaceThumbnails { get; set; } = new();
    public int FaceCount => Faces.Count;
    public int PhotoCount => Faces.Select(f => f.PhotoId).Distinct().Count();
}

public class InputDialog : Window
{
    private System.Windows.Controls.TextBox txtInput = new System.Windows.Controls.TextBox();
    private System.Windows.Controls.Button btnOk = new System.Windows.Controls.Button();
    private System.Windows.Controls.Button btnCancel = new System.Windows.Controls.Button();

    public string Answer { get; private set; } = string.Empty;

    public InputDialog(string title, string prompt, string defaultValue = "")
    {
        Title = title;
        Width = 400;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(37, 37, 37));

        var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };

        stackPanel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = prompt,
            Foreground = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 0, 0, 10),
            FontSize = 13
        });

        txtInput.Text = defaultValue;
        txtInput.Margin = new Thickness(0, 0, 0, 20);
        txtInput.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45));
        txtInput.Foreground = System.Windows.Media.Brushes.White;
        txtInput.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
        stackPanel.Children.Add(txtInput);

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        btnOk.Content = "Aceptar";
        btnOk.Width = 80;
        btnOk.Margin = new Thickness(0, 0, 10, 0);
        btnOk.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
        btnOk.Foreground = System.Windows.Media.Brushes.White;
        btnOk.BorderThickness = new Thickness(0);
        btnOk.Cursor = System.Windows.Input.Cursors.Hand;
        btnOk.Click += (s, e) => { Answer = txtInput.Text; DialogResult = true; };
        buttonPanel.Children.Add(btnOk);

        btnCancel.Content = "Cancelar";
        btnCancel.Width = 80;
        btnCancel.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45));
        btnCancel.Foreground = System.Windows.Media.Brushes.White;
        btnCancel.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
        btnCancel.Cursor = System.Windows.Input.Cursors.Hand;
        btnCancel.Click += (s, e) => { DialogResult = false; };
        buttonPanel.Children.Add(btnCancel);

        stackPanel.Children.Add(buttonPanel);
        Content = stackPanel;
    }
}