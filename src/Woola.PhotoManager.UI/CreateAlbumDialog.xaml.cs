using System.Windows;

namespace Woola.PhotoManager.UI;

public partial class CreateAlbumDialog : Window
{
    public string? AlbumName        => NameBox.Text.Trim();
    public string? AlbumDescription => string.IsNullOrWhiteSpace(DescBox.Text) ? null : DescBox.Text.Trim();

    public CreateAlbumDialog() => InitializeComponent();

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            NameBox.BorderBrush = System.Windows.Media.Brushes.OrangeRed;
            NameBox.Focus();
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
