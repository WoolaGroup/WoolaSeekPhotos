using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Woola.PhotoManager.Frontend.MAUI.Services;

namespace Woola.PhotoManager.Frontend.MAUI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddMudServices();

        builder.Services.AddSingleton<PhotoApiClient>(_ =>
        {
            var http = new HttpClient();
            return new PhotoApiClient(http);
        });
        builder.Services.AddSingleton<BackendConfigService>();

        builder.Services.AddSingleton<IAuthService, AuthService>();
        builder.Services.AddSingleton<IPhotoService, PhotoService>();
        builder.Services.AddSingleton<IAlbumService, AlbumService>();
        builder.Services.AddSingleton<IDashboardService, DashboardService>();
        builder.Services.AddSingleton<ITagService, TagService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
