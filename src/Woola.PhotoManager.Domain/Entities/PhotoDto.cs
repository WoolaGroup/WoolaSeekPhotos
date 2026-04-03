using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Woola.PhotoManager.Domain.Entities
{
    public class PhotoDto
    {
        public int Id { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public long? DateTaken { get; set; }  // ← long de la DB
        public int Width { get; set; }
        public int Height { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ThumbnailPath { get; set; }
        public long CreatedAt { get; set; }
        public long? LastIndexedAt { get; set; }
    }
}
