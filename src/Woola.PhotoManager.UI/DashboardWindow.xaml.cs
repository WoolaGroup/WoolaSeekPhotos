using System.Windows;
using Woola.PhotoManager.Infrastructure.Repositories;
using MessageBox = System.Windows.MessageBox;

namespace Woola.PhotoManager.UI;

public partial class DashboardWindow : Window
{
    private readonly StatsRepository _stats;

    public DashboardWindow(StatsRepository stats)
    {
        _stats = stats;
        InitializeComponent();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var (overview, topTags, months, sources, today) = await LoadAllStatsAsync();

            Dispatcher.Invoke(() =>
            {
                // Tarjetas resumen
                TotalPhotosText.Text = overview.Photos.ToString("N0");
                TotalTagsText.Text   = overview.Tags.ToString("N0");
                TotalFacesText.Text  = overview.Faces.ToString("N0");
                TotalAlbumsText.Text = overview.Albums.ToString("N0");

                // Top tags (con MaxCount para barra proporcional)
                var maxTag = topTags.Count > 0 ? topTags.Max(t => t.Count) : 1;
                TopTagsList.ItemsSource = topTags
                    .Select(t => new { t.Name, t.Count, MaxCount = maxTag })
                    .ToList();

                // Fotos por mes
                var maxMonth = months.Count > 0 ? months.Max(m => m.Count) : 1;
                MonthList.ItemsSource = months
                    .Select(m => new
                    {
                        Month    = FormatMonth(m.Month),
                        m.Count,
                        MaxCount = maxMonth
                    })
                    .ToList();

                // Tags por agente
                var maxSrc = sources.Count > 0 ? sources.Max(s => s.Count) : 1;
                SourceList.ItemsSource = sources
                    .Select(s => new { s.Source, s.Count, MaxCount = maxSrc })
                    .ToList();

                // Pie
                FooterText.Text = $"Fotos indexadas hoy: {today}";
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error cargando dashboard: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<(StatsOverview overview,
                         List<TagStat> topTags,
                         List<MonthStat> months,
                         List<SourceStat> sources,
                         int today)> LoadAllStatsAsync()
    {
        var overview = await _stats.GetOverviewAsync();
        var topTags  = await _stats.GetTopTagsAsync(15);
        var months   = await _stats.GetPhotosByMonthAsync(12);
        var sources  = await _stats.GetTagsBySourceAsync();
        var today    = await _stats.GetPhotosIndexedTodayAsync();
        return (overview, topTags, months, sources, today);
    }

    private static string FormatMonth(string yyyyMM)
    {
        if (DateTime.TryParseExact(yyyyMM + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
            return dt.ToString("MMM yyyy", new System.Globalization.CultureInfo("es-ES"));
        return yyyyMM;
    }
}
