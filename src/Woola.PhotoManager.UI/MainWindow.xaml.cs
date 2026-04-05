using System.Windows;
using Woola.PhotoManager.Core.Agents;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Infrastructure.Repositories;
using Woola.PhotoManager.UI.ViewModels;

namespace Woola.PhotoManager.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel       _vm;
    private readonly AlbumRepository     _albumRepository;
    private readonly PhotoRepository     _photoRepository;
    private readonly StatsRepository     _statsRepository;
    private readonly TagRepository       _tagRepository;
    private readonly IAgentOrchestrator  _orchestrator;
    private readonly ISettingsService    _settingsService;
    private readonly IDuplicateDetectionService _duplicateService;

    public MainWindow(MainViewModel vm,
                      AlbumRepository albumRepository,
                      PhotoRepository photoRepository,
                      StatsRepository statsRepository,
                      TagRepository tagRepository,
                      IAgentOrchestrator orchestrator,
                      ISettingsService settingsService,
                      IDuplicateDetectionService duplicateService)
    {
        InitializeComponent();
        _vm               = vm;
        _albumRepository  = albumRepository;
        _photoRepository  = photoRepository;
        _statsRepository  = statsRepository;
        _tagRepository    = tagRepository;
        _orchestrator     = orchestrator;
        _settingsService  = settingsService;
        _duplicateService = duplicateService;
        DataContext       = vm;

        vm.ErrorOccurred += (_, msg) =>
            System.Windows.MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private async void AlbumsBtn_Click(object sender, RoutedEventArgs e)
    {
        new AlbumWindow(_albumRepository, _photoRepository, _tagRepository) { Owner = this }.ShowDialog();
        await _vm.RefreshAlbumsAsync();
    }

    private void DashboardBtn_Click(object sender, RoutedEventArgs e)
        => new DashboardWindow(_statsRepository) { Owner = this }.ShowDialog();

    private void FaceManagementBtn_Click(object sender, RoutedEventArgs e)
        => new FaceManagementWindow { Owner = this }.ShowDialog();

    private void TestVisionBtn_Click(object sender, RoutedEventArgs e)
        => new TestVisionWindow { Owner = this }.ShowDialog();

    private void TestOcrBtn_Click(object sender, RoutedEventArgs e)
        => new TestOcrWindow { Owner = this }.ShowDialog();

    private void PresentationBtn_Click(object sender, RoutedEventArgs e)
    {
        var paths = _vm.Photos
            .Select(p => p.ThumbnailPath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();

        if (paths.Count == 0)
        {
            System.Windows.MessageBox.Show("No hay fotos para mostrar", "Modo Presentación",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        new PresentationWindow(paths!) { Owner = this }.ShowDialog();
    }

    // ── IMP-005: Duplicados ───────────────────────────────────────────────────

    private void DuplicatesBtn_Click(object sender, RoutedEventArgs e)
        => new DuplicatesWindow(_photoRepository) { Owner = this }.ShowDialog();

    // ── IMP-010: Configuración ────────────────────────────────────────────────

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        => new SettingsWindow(_orchestrator, _settingsService) { Owner = this }.ShowDialog();

    // ─────────────────────────────────────────────────────────────────────────

    private async void ReprocessTagsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.MessageBox.Show(
            "¿Reprocesar tags automáticos para TODAS las fotos?",
            "Reprocesar Tags",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        ReprocessTagsBtn.IsEnabled = false;
        try
        {
            await _vm.ReprocessTagsCommand.ExecuteAsync(null);
            System.Windows.MessageBox.Show($"Tags reprocesados correctamente.",
                "Completado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            ReprocessTagsBtn.IsEnabled = true;
        }
    }
}
