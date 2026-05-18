namespace Woola.PhotoManager.Shared.Configuration;

public class WoolaOptions
{
    public const string SectionName = "WoolaPhotos";

    public string DatabasePath { get; set; } = string.Empty;
    public string ThumbnailDirectory { get; set; } = string.Empty;
    public string UploadDirectory { get; set; } = string.Empty;
    public string[] AllowedExtensions { get; set; } = { ".jpg", ".jpeg", ".png", ".bmp", ".tiff" };
    public int ThumbnailSize { get; set; } = 512;
    public int JpegQuality { get; set; } = 80;
    public int MaxUploadSizeMb { get; set; } = 50;
    public int DefaultPageSize { get; set; } = 50;
    public int ThumbnailCacheMinutes { get; set; } = 60;
}

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "WoolaPhotos";
    public string Audience { get; set; } = "WoolaPhotos";
    public int ExpiryHours { get; set; } = 24;
}

public class AuthOptions
{
    public const string SectionName = "Auth";

    public string DefaultUsername { get; set; } = "admin";
    public string DefaultPassword { get; set; } = "woola2024";
}

public class FrontendOptions
{
    public const string SectionName = "WoolaPhotos";

    public string BackendUrl { get; set; } = "http://localhost:5150";
    public int RequestTimeoutSeconds { get; set; } = 30;
}
