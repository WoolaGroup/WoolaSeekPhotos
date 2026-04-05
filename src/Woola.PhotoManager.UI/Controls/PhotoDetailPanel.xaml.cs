using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.UI.ViewModels;

namespace Woola.PhotoManager.UI.Controls;

public partial class PhotoDetailPanel : System.Windows.Controls.UserControl
{
    public PhotoDetailPanel() => InitializeComponent();

    // IMP-009: Abrir ventana de fotos similares usando el servicio de similitud
    private void SimilarPhotosBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PhotoDetailViewModel vm) return;

        var service = App.Services.GetRequiredService<ISimilarPhotosService>();
        new SimilarPhotosWindow(service, vm.Id, vm.FileName) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
