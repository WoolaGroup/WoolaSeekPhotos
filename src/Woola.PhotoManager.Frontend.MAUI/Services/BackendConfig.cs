namespace Woola.PhotoManager.Frontend.MAUI.Services;

public static class BackendConfig
{
    public static string BaseUrl { get; set; } = "http://localhost:5150";
}

public class BackendConfigService
{
    public string BaseUrl => BackendConfig.BaseUrl;
}
