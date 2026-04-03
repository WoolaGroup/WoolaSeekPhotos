using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Woola.PhotoManager.Common.Models;
using MetadataExtractor.Formats.Iptc;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;
using MetadataExtractor.Formats.Tiff;
using MetadataExtractor.Formats.Bmp;
using MetadataExtractor.Formats.Gif;
namespace Woola.PhotoManager.Common.Services;

public class MetadataService : IMetadataService
{
    public async Task<PhotoMetadata> ExtractMetadataAsync(string imagePath)
    {
        var metadata = new PhotoMetadata();

        await Task.Run(() =>
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(imagePath);

                // Extraer fecha EXIF
                var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (subIfdDirectory != null)
                {
                    DateTime? dateTimeOriginal = subIfdDirectory.GetDateTime(ExifDirectoryBase.TagDateTimeOriginal);
                    if (dateTimeOriginal.HasValue)
                    {
                        metadata.DateTaken = dateTimeOriginal.Value;
                    }
                }

                // Extraer GPS (manejo seguro)
                GpsDirectory? gpsDirectory = directories.OfType<GpsDirectory>().FirstOrDefault();
                if (gpsDirectory != null)
                {
                    GeoLocation? location = gpsDirectory.GetGeoLocation();
                    if (location != null && location.HasValue)
                    {
                        metadata.Latitude = location.Value.Latitude;
                        metadata.Longitude = location.Value.Longitude;
                    }
                }

                // Extraer información de cámara
                var ifd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (ifd0Directory != null)
                {
                    metadata.CameraModel = ifd0Directory.GetDescription(ExifDirectoryBase.TagModel);
                    int? orientation = ifd0Directory.GetInt32(ExifDirectoryBase.TagOrientation);
                    if (orientation.HasValue)
                        metadata.Orientation = orientation.Value;
                }

                // Extraer parámetros de disparo
                if (subIfdDirectory != null)
                {
                    metadata.LensModel = subIfdDirectory.GetDescription(ExifSubIfdDirectory.TagLensModel);

                    double? aperture = subIfdDirectory.GetDouble(ExifSubIfdDirectory.TagFNumber);
                    if (aperture.HasValue)
                        metadata.Aperture = Math.Round(aperture.Value, 1);

                    int? iso = subIfdDirectory.GetInt32(ExifSubIfdDirectory.TagIsoEquivalent);
                    if (iso.HasValue)
                        metadata.Iso = iso.Value;

                    int? focalLength = subIfdDirectory.GetInt32(ExifSubIfdDirectory.TagFocalLength);
                    if (focalLength.HasValue)
                        metadata.FocalLength = focalLength.Value;

                    double? shutterSpeed = subIfdDirectory.GetDouble(ExifSubIfdDirectory.TagExposureTime);
                    if (shutterSpeed.HasValue)
                    {
                        if (shutterSpeed.Value >= 1)
                            metadata.ShutterSpeed = Math.Round(shutterSpeed.Value, 1);
                        else
                            metadata.ShutterSpeed = 1.0 / Math.Round(1.0 / shutterSpeed.Value);
                    }
                }
            }
            catch
            {
                // Si falla, devolver metadata vacío
            }
        });

        return metadata;
    }
}