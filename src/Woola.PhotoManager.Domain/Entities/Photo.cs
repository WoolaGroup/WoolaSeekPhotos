public class Photo
{
    public int Id { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public long FileSize { get; set; }

    // Para Dapper - lectura desde DB
    public string? DateTakenString { get; set; }
    public string? CreatedAtString { get; set; }
    public string? LastIndexedAtString { get; set; }

    // Propiedades de conveniencia
    public DateTime? DateTaken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // ← Siempre tiene valor
    public DateTime? LastIndexedAt { get; set; }

    public int Width { get; set; }
    public int Height { get; set; }
    public string Status { get; set; } = "Discovered";
    public string? ThumbnailPath { get; set; }

    public string FileName => System.IO.Path.GetFileName(Path);
}