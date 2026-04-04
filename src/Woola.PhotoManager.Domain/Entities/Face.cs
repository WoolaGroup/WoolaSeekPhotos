namespace Woola.PhotoManager.Domain.Entities;

public class Face
{
    public int Id { get; set; }
    public int PhotoId { get; set; }
    public string? PersonName { get; set; }
    public string? PersonId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[]? Encoding { get; set; }  // ← byte[], no string
    public double Confidence { get; set; }
    public bool IsUserConfirmed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Eliminar EncodingString si existe
    public Photo? Photo { get; set; }
}