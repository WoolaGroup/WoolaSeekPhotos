using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Woola.PhotoManager.Domain.Entities;

public class Face
{
    public int Id { get; set; }
    public int PhotoId { get; set; }
    public string? PersonName { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[]? Encoding { get; set; }
    public double Confidence { get; set; }
    public bool IsUserConfirmed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Photo? Photo { get; set; }
}
