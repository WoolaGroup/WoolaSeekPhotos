using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Woola.PhotoManager.Core.Agents;
// Alias para resolver ambigüedad con System.Windows.Forms
using Button        = System.Windows.Controls.Button;
using KeyEventArgs  = System.Windows.Input.KeyEventArgs;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Infrastructure.Repositories;
using Woola.PhotoManager.UI.ViewModels;

namespace Woola.PhotoManager.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel              _vm;
    private readonly AlbumRepository            _albumRepository;
    private readonly PhotoRepository            _photoRepository;
    private readonly StatsRepository            _statsRepository;
    private readonly TagRepository              _tagRepository;
    private readonly IAgentOrchestrator         _orchestrator;
    private readonly ISettingsService           _settingsService;
    private readonly IDuplicateDetectionService _duplicateService;

    private Button? _activeTab;

    public MainWindow(MainViewModel vm,
                      AlbumRepository albumRepository,
                      PhotoRepository photoRepository,
                      StatsRepository statsRepository,
                      TagRepository tagRepository,
                      IAgentOrchestrator orchestrator,
                      ISettingsService settingsService,
                      IDuplicateDetectionService duplicateService)
    {
        // ⚠ Asignar ANTES de InitializeComponent para que los handlers que se
        //   disparan durante la carga del XAML (p.ej. SortCombo SelectionChanged)
        //   encuentren los campos ya inicializados.
        _vm               = vm;
        _albumRepository  = albumRepository;
        _photoRepository  = photoRepository;
        _statsRepository  = statsRepository;
        _tagRepository    = tagRepository;
        _orchestrator     = orchestrator;
        _settingsService  = settingsService;
        _duplicateService = duplicateService;

        InitializeComponent();

        DataContext = vm;

        vm.ErrorOccurred += (_, msg) =>
            System.Windows.MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);

        Loaded += (_, _) => SetActiveTab(TabBiblioteca);
    }

    // ── Tab navigation ────────────────────────────────────────────────────────

    private void SetActiveTab(Button tab)
    {
        var accent  = (System.Windows.Media.Brush)FindResource("BrushAccent");
        var primary = (System.Windows.Media.Brush)FindResource("BrushTextPrimary");
        var muted   = (System.Windows.Media.Brush)FindResource("BrushTextMuted");

        foreach (var t in new[] { TabBiblioteca, TabAlbumes, TabRostros, TabDiscover })
        {
            t.Foreground      = muted;
            t.BorderThickness = new Thickness(0);
            t.BorderBrush     = System.Windows.Media.Brushes.Transparent;
        }

        tab.Foreground      = primary;
        tab.BorderThickness = new Thickness(0, 0, 0, 2);
        tab.BorderBrush     = accent;
        _activeTab          = tab;
    }

    private void TabBiblioteca_Click(object sender, RoutedEventArgs e)
        => SetActiveTab(TabBiblioteca);

    private async void TabAlbumes_Click(object sender, RoutedEventArgs e)
    {
        SetActiveTab(TabAlbumes);
        new AlbumWindow(_albumRepository, _photoRepository, _tagRepository) { Owner = this }.ShowDialog();
        await _vm.RefreshAlbumsAsync();
    }

    private void TabRostros_Click(object sender, RoutedEventArgs e)
    {
        SetActiveTab(TabRostros);
        new FaceManagementWindow { Owner = this }.ShowDialog();
    }

    private void TabDiscover_Click(object sender, RoutedEventArgs e)
    {
        SetActiveTab(TabDiscover);
        SearchBox.Focus();
        SearchBox.SelectAll();
        if (string.IsNullOrWhiteSpace(_vm.SearchText))
            _vm.StatusText = "Discover: escribe una búsqueda semántica y presiona Enter";
        else
            _vm.HybridSearchCommand.Execute(null);
    }

    // ── Sort ──────────────────────────────────────────────────────────────────

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SortCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag &&
            Enum.TryParse<SortMode>(tag, out var mode))
        {
            _vm.SortMode = mode;
        }
    }

    // ── Keyboard navigation ───────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        switch (e.Key)
        {
            case Key.Escape:
                if (_vm.SelectedPhoto != null)
                {
                    _vm.CloseDetailCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.Right:
            case Key.Down:
                if (_vm.SelectedPhoto != null)
                {
                    _vm.NavigateNextPhotoCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.Left:
            case Key.Up:
                if (_vm.SelectedPhoto != null)
                {
                    _vm.NavigatePrevPhotoCommand.Execute(null);
                    e.Handled = true;
                }
                break;

            case Key.F:
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    SearchBox.Focus();
                    SearchBox.SelectAll();
                    e.Handled = true;
                }
                break;
        }
    }

    // ── Existing handlers ─────────────────────────────────────────────────────

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

    private void DuplicatesBtn_Click(object sender, RoutedEventArgs e)
        => new DuplicatesWindow(_photoRepository) { Owner = this }.ShowDialog();

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        => new SettingsWindow(_orchestrator, _settingsService) { Owner = this }.ShowDialog();

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
            System.Windows.MessageBox.Show("Tags reprocesados correctamente.",
                "Completado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            ReprocessTagsBtn.IsEnabled = true;
        }
    }
}
