using System.Windows;
using Woola.PhotoManager.Core.Agents;
using Woola.PhotoManager.Core.Services;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

/// <summary>
/// IMP-010: Ventana de configuración — activar/desactivar agentes + umbral clustering facial.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly ISettingsService _settingsService;
    private AppSettings _settings;

    public SettingsWindow(IAgentOrchestrator orchestrator, ISettingsService settingsService)
    {
        InitializeComponent();
        _orchestrator = orchestrator;
        _settingsService = settingsService;

        _settings = _settingsService.Load();
        LoadAgents();
        LoadThreshold();
    }

    private void LoadAgents()
    {
        var agents = _orchestrator.GetAgents();

        // Aplicar configuración guardada al estado actual de los agentes
        foreach (var agent in agents)
        {
            if (_settings.AgentEnabled.TryGetValue(agent.Name, out var enabled))
                agent.IsEnabled = enabled;
            // Si no hay configuración → mantener el valor actual (default del agente)
        }

        AgentsList.ItemsSource = agents;
    }

    private void LoadThreshold()
    {
        ThresholdSlider.Value = _settings.FaceClusterThreshold;
        ThresholdValueText.Text = $"{_settings.FaceClusterThreshold:F2}";
    }

    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdValueText != null)
            ThresholdValueText.Text = $"{e.NewValue:F2}";
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        // Actualizar settings desde estado actual de la lista
        var agents = _orchestrator.GetAgents();
        _settings.AgentEnabled = agents.ToDictionary(a => a.Name, a => a.IsEnabled);
        _settings.FaceClusterThreshold = (float)ThresholdSlider.Value;

        // Aplicar al orquestador en caliente
        foreach (var agent in agents)
            _orchestrator.EnableAgent(agent.Name, agent.IsEnabled);

        // Persistir en disco
        _settingsService.Save(_settings);

        DialogResult = true;
        Close();
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        // Restaurar estado original de los agentes (revertir cambios de UI)
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
