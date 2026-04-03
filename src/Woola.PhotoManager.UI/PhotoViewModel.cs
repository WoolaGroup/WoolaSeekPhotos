using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.UI
{
    public class PhotoViewModel
    {
        private readonly Photo _photo;

        public PhotoViewModel(Photo photo)
        {
            _photo = photo;
        }

        public string ThumbnailPath => _photo.ThumbnailPath ?? "";
        public string FileName => _photo.FileName;
        public DateTime? DateTaken => _photo.DateTaken;
        public string CameraModel => _photo.CameraModel ?? "📷 Cámara desconocida";
    }
}
