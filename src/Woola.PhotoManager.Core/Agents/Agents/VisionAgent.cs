using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

public class VisionAgent : IAgent
{
    private readonly IObjectDetectionService _objectDetectionService;

    public string Name => "VisionAgent";
    public string Description => "Detecta objetos en imágenes usando YOLO";
    public int Priority => 3;
    public bool IsEnabled { get; set; } = true;

    public VisionAgent(IObjectDetectionService objectDetectionService)
    {
        _objectDetectionService = objectDetectionService;
    }

    public bool CanProcess(Photo photo)
    {
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
        var ext = Path.GetExtension(photo.Path).ToLower();
        return extensions.Contains(ext) && File.Exists(photo.Path);
    }

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            await _objectDetectionService.DownloadModelIfNeededAsync();

            var detections = await _objectDetectionService.DetectObjectsAsync(photo.Path);

            foreach (var detection in detections)
            {
                result.Tags.Add(new AgentTag
                {
                    Name = detection.ClassName,
                    Category = "Object",
                    Confidence = detection.Confidence,
                    Source = Name
                });
            }

            if (detections.Any(d => d.ClassName == "persona"))
            {
                result.Tags.Add(new AgentTag
                {
                    Name = "contiene_personas",
                    Category = "Scene",
                    Confidence = 0.9,
                    Source = Name
                });
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