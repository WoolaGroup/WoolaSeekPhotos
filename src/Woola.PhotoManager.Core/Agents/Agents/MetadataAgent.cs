using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

public class MetadataAgent : IAgent
{
    private readonly IMetadataService _metadataService;

    public string Name => "MetadataAgent";
    public string Description => "Extrae metadatos EXIF y genera tags de contexto técnico";
    public int Priority => 1;
    public bool IsEnabled { get; set; } = true;

    public MetadataAgent(IMetadataService metadataService)
    {
        _metadataService = metadataService;
    }

    public bool CanProcess(Photo photo) => File.Exists(photo.Path);

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            var metadata = await _metadataService.ExtractMetadataAsync(photo.Path);

            // ── Actualizar TODOS los campos EXIF del entity ───────────────────
            if (metadata.DateTaken.HasValue)    photo.DateTaken    = metadata.DateTaken.Value;
            if (metadata.Latitude.HasValue)     photo.Latitude     = metadata.Latitude;
            if (metadata.Longitude.HasValue)    photo.Longitude    = metadata.Longitude;
            if (!string.IsNullOrEmpty(metadata.CameraModel)) photo.CameraModel = metadata.CameraModel;
            if (!string.IsNullOrEmpty(metadata.LensModel))   photo.LensModel   = metadata.LensModel;
            if (metadata.Aperture.HasValue)     photo.Aperture     = metadata.Aperture;
            if (metadata.ShutterSpeed.HasValue) photo.ShutterSpeed = metadata.ShutterSpeed;
            if (metadata.Iso.HasValue)          photo.Iso          = metadata.Iso;
            if (metadata.FocalLength.HasValue)  photo.FocalLength  = metadata.FocalLength;
            if (metadata.Orientation.HasValue)  photo.Orientation  = metadata.Orientation;

            // ── Generar tags de contexto técnico ──────────────────────────────
            void AddTag(string name, string category, float confidence = 0.9f) =>
                result.Tags.Add(new AgentTag { Name = name, Category = category,
                                               Confidence = confidence, Source = Name });

            // Hora del día (local) desde DateTaken EXIF
            var refDate = metadata.DateTaken?.ToLocalTime() ?? photo.DateTaken?.ToLocalTime();
            if (refDate.HasValue)
            {
                int hour = refDate.Value.Hour;
                if      (hour is >= 5 and <= 8)  AddTag("amanecer",  "Time");
                else if (hour is >= 9 and <= 12) AddTag("mañana",    "Time");
                else if (hour is >= 13 and <= 16) AddTag("tarde",    "Time");
                else if (hour is >= 17 and <= 20) AddTag("atardecer","Time");
                else                              AddTag("noche",     "Time");
            }

            // Calidad de luz según ISO
            if (metadata.Iso.HasValue)
            {
                if (metadata.Iso.Value > 3200)
                    AddTag("alta_iso", "Technical", 0.85f);
                else if (metadata.Iso.Value is > 0 and <= 400)
                    AddTag("buena_luz", "Technical", 0.80f);
            }

            // Lente disponible
            if (!string.IsNullOrEmpty(metadata.LensModel))
                AddTag("con_lente_info", "Technical", 1.0f);

            // GPS
            if (metadata.HasGps)
                AddTag("con_gps", "Location", 1.0f);

            result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}
