namespace Woola.PhotoManager.Backend.Domain.Entities;

public class Face : BaseEntity
{
    public int PhotoId { get; private set; }
    public string? PersonName { get; private set; }
    public string? PersonId { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public byte[]? Encoding { get; private set; }
    public double Confidence { get; private set; }
    public bool IsUserConfirmed { get; private set; }

    public Photo? Photo { get; private set; }

    public static Face Create(
        int photoId, int x, int y, int width, int height,
        double confidence, byte[]? encoding = null)
    {
        return new Face
        {
            PhotoId = photoId,
            X = x, Y = y,
            Width = width, Height = height,
            Confidence = confidence,
            Encoding = encoding,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetPerson(string? name, string? personId)
    {
        PersonName = name;
        PersonId = personId;
        IsUserConfirmed = !string.IsNullOrEmpty(name);
        Touch();
    }

    public void ConfirmPerson(string name)
    {
        PersonName = name;
        IsUserConfirmed = true;
        Touch();
    }
}
