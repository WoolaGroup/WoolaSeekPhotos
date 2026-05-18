namespace Woola.PhotoManager.Shared.Enums;

public enum SortMode
{
    DateDesc,
    DateAsc,
    NameAsc,
    SizeDesc,
    CameraModel
}

public enum PhotoStatus
{
    Discovered,
    Indexed,
    Analyzing,
    Analyzed,
    Favorite,
    Duplicate
}

public enum FilterType
{
    None,
    Album,
    Tag,
    Event
}

public enum AgentName
{
    MetadataAgent,
    AutoTaggingAgent,
    VisionAgent,
    FaceAgent,
    OcrAgent,
    SceneAgent,
    QualityAgent,
    GeoLocationAgent,
    ClaudeVisionAgent
}

public static class AgentNameExtensions
{
    public static string ToDisplayName(this AgentName agent) => agent switch
    {
        AgentName.MetadataAgent => "Metadata",
        AgentName.AutoTaggingAgent => "Auto Tags",
        AgentName.VisionAgent => "Vision (YOLO)",
        AgentName.FaceAgent => "Face Detection",
        AgentName.OcrAgent => "OCR",
        AgentName.SceneAgent => "Scene Analysis",
        AgentName.QualityAgent => "Quality Assessment",
        AgentName.GeoLocationAgent => "Geo Location",
        AgentName.ClaudeVisionAgent => "Claude Vision",
        _ => agent.ToString()
    };

    public static string ToDescription(this AgentName agent) => agent switch
    {
        AgentName.MetadataAgent => "Extracts EXIF metadata (camera, lens, GPS, etc.)",
        AgentName.AutoTaggingAgent => "Generates year/month/season/decade/camera tags",
        AgentName.VisionAgent => "YOLO object detection + color detection",
        AgentName.FaceAgent => "Face detection + recognition + clustering",
        AgentName.OcrAgent => "Optical character recognition (Tesseract)",
        AgentName.SceneAgent => "Infers scenes from tags and brightness",
        AgentName.QualityAgent => "Blur/exposure/resolution assessment",
        AgentName.GeoLocationAgent => "Reverse geocoding for GPS coordinates",
        AgentName.ClaudeVisionAgent => "Anthropic Claude API for scene analysis",
        _ => ""
    };
}

public enum SmartAlbumId
{
    NoAlbum,
    Recent,
    Favorites,
    NoLocation,
    NoTags
}

public static class SmartAlbumIdExtensions
{
    public static string ToRouteString(this SmartAlbumId id) => id switch
    {
        SmartAlbumId.NoAlbum => "no-album",
        SmartAlbumId.Recent => "recent",
        SmartAlbumId.Favorites => "favorites",
        SmartAlbumId.NoLocation => "no-location",
        SmartAlbumId.NoTags => "no-tags",
        _ => id.ToString().ToLower()
    };

    public static string ToDisplayName(this SmartAlbumId id) => id switch
    {
        SmartAlbumId.NoAlbum => "Sin álbum",
        SmartAlbumId.Recent => "Recién añadidas",
        SmartAlbumId.Favorites => "Favoritas",
        SmartAlbumId.NoLocation => "Sin ubicación",
        SmartAlbumId.NoTags => "Sin etiquetas",
        _ => id.ToString()
    };
}
