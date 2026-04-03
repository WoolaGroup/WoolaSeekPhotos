using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

public class MetadataAgent : IAgent
{
    private readonly IMetadataService _metadataService;

    public string Name => "MetadataAgent";
    public string Description => "Extrae metadatos EXIF de las imágenes";
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

            if (metadata.DateTaken.HasValue)
            {
                photo.DateTaken = metadata.DateTaken.Value;
            }

            if (metadata.Latitude.HasValue && metadata.Longitude.HasValue)
            {
                photo.Latitude = metadata.Latitude;
                photo.Longitude = metadata.Longitude;
            }

            if (!string.IsNullOrEmpty(metadata.CameraModel))
            {
                photo.CameraModel = metadata.CameraModel;
            }

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