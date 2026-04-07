using System.Windows;
using Woola.PhotoManager.Core.Agents;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.UI.Services;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

/// <summary>
/// IMP-010: Ventana de configuración — agentes, clustering facial, importación cloud y AI.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IAgentOrchestrator   _orchestrator;
    private readonly ISettingsService     _settingsService;
    private readonly IFolderPickerService _folderPicker;
    private AppSettings _settings;

    public SettingsWindow(
        IAgentOrchestrator orchestrator,
        ISettingsService settingsService,
        IFolderPickerService folderPicker)
    {
        InitializeComponent();
        _orchestrator    = orchestrator;
        _settingsService = settingsService;
        _folderPicker    = folderPicker;

        _settings = _settingsService.Load();
        LoadAgents();
        LoadThreshold();
        LoadCloudSettings();
        LoadAiSettings();
    }

    // ── Agentes ───────────────────────────────────────────────────────────────

    private void LoadAgents()
    {
        var agents = _orchestrator.GetAgents();

        foreach (var agent in agents)
        {
            if (_settings.AgentEnabled.TryGetValue(agent.Name, out var enabled))
                agent.IsEnabled = enabled;
        }

        AgentsList.ItemsSource = agents;
    }

    // ── Clustering facial ─────────────────────────────────────────────────────

    private void LoadThreshold()
    {
        ThresholdSlider.Value   = _settings.FaceClusterThreshold;
        ThresholdValueText.Text = $"{_settings.FaceClusterThreshold:F2}";
    }

    private void ThresholdSlider_ValueChanged(
        object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdValueText != null)
            ThresholdValueText.Text = $"{e.NewValue:F2}";
    }

    // ── Importación cloud ─────────────────────────────────────────────────────

    private void LoadCloudSettings()
    {
        ImportDestPathBox.Text  = _settings.ImportDestinationPath;
        GoogleDrivePathBox.Text = _settings.GoogleDrivePath ?? string.Empty;
    }

    private void BrowseDestBtn_Click(object sender, RoutedEventArgs e)
    {
        var folder = _folderPicker.PickFolder("Carpeta destino para fotos importadas");
        if (folder != null)
            ImportDestPathBox.Text = folder;
    }

    private void BrowseGDriveBtn_Click(object sender, RoutedEventArgs e)
    {
        var folder = _folderPicker.PickFolder("Seleccionar carpeta local de Google Drive");
        if (folder != null)
            GoogleDrivePathBox.Text = folder;
    }

    // ── Análisis AI ───────────────────────────────────────────────────────────

    private void LoadAiSettings()
    {
        AnthropicApiKeyBox.Text = _settings.AnthropicApiKey ?? string.Empty;
    }

    // ── Guardar / Cancelar ────────────────────────────────────────────────────

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        // Agentes
        var agents = _orchestrator.GetAgents();
        _settings.AgentEnabled = agents.ToDictionary(a => a.Name, a => a.IsEnabled);

        foreach (var agent in agents)
            _orchestrator.EnableAgent(agent.Name, agent.IsEnabled);

        // Clustering
        _settings.FaceClusterThreshold = (float)ThresholdSlider.Value;

        // Cloud
        _settings.ImportDestinationPath = ImportDestPathBox.Text;
        _settings.GoogleDrivePath = string.IsNullOrWhiteSpace(GoogleDrivePathBox.Text)
            ? null
            : GoogleDrivePathBox.Text;

        // AI
        _settings.AnthropicApiKey = string.IsNullOrWhiteSpace(AnthropicApiKeyBox.Text)
            ? null
            : AnthropicApiKeyBox.Text;

        _settingsService.Save(_settings);

        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        var agents = _orchestrator.GetAgents();
        foreach (var agent in agents)
        {
            if (_settings.AgentEnabled.TryGetValue(agent.Name, out var enabled))
                _orchestrator.EnableAgent(agent.Name, enabled);
        }

        DialogResult = false;
        Close();
    }
}
