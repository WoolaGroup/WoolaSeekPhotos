using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Woola.PhotoManager.Domain.Entities;

public class PhotoTag
{
    public int PhotoId { get; set; }
    public int TagId { get; set; }
    public double Confidence { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Photo? Photo { get; set; }
    public Tag? Tag { get; set; }
}
