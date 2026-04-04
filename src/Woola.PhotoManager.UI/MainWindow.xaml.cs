using System.Windows;
using Woola.PhotoManager.UI.ViewModels;

namespace Woola.PhotoManager.UI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        vm.ErrorOccurred += (_, msg) =>
            System.Windows.MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

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
