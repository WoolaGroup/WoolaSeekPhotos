using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.UI.ViewModels;

public class PhotoViewModel
{
    private readonly Photo _photo;

    public PhotoViewModel(Photo photo) => _photo = photo;

    public string ThumbnailPath => _photo.ThumbnailPath ?? "";
    public string FileName => _photo.FileName;
    public DateTime? DateTaken => _photo.DateTaken;
    public string CameraModel => _photo.CameraModel ?? "📷 Cámara desconocida";
    public int Id => _photo.Id;
}
