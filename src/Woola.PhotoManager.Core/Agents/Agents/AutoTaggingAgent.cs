using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Agents.Agents;

public class AutoTaggingAgent : IAgent
{
    private readonly TagRepository _tagRepository;

    public string Name => "AutoTaggingAgent";
    public string Description => "Genera tags automáticos por fecha, ubicación y cámara";
    public int Priority => 2;
    public bool IsEnabled { get; set; } = true;

    public AutoTaggingAgent(TagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public bool CanProcess(Photo photo) => true;

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            var dateToUse = photo.DateTaken ?? photo.CreatedAt;

            // Tag por año
            result.Tags.Add(new AgentTag
            {
                Name = $"año_{dateToUse.Year}",
                Category = "Date",
                Confidence = 1.0,
                Source = Name
            });

            // Tag por mes
            var monthNames = new[] { "enero", "febrero", "marzo", "abril", "mayo", "junio",
                                      "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };
            result.Tags.Add(new AgentTag
            {
                Name = $"mes_{monthNames[dateToUse.Month - 1]}",
                Category = "Date",
                Confidence = 1.0,
                Source = Name
            });

            // Tag por estación
            string season = GetSeason(dateToUse);
            result.Tags.Add(new AgentTag
            {
                Name = $"estación_{season}",
                Category = "Date",
                Confidence = 1.0,
                Source = Name
            });

            // Tag por década
            int decade = (dateToUse.Year / 10) * 10;
            result.Tags.Add(new AgentTag
            {
                Name = $"década_{decade}s",
                Category = "Date",
                Confidence = 1.0,
                Source = Name
            });

            // Tag por cámara
            if (!string.IsNullOrEmpty(photo.CameraModel))
            {
                var cameraBrand = GetCameraBrand(photo.CameraModel);
                result.Tags.Add(new AgentTag
                {
                    Name = $"cámara_{cameraBrand}",
                    Category = "Camera",
                    Confidence = 1.0,
                    Source = Name
                });
            }

            // Tag por GPS
            if (photo.Latitude.HasValue && photo.Longitude.HasValue)
            {
                result.Tags.Add(new AgentTag
                {
                    Name = "con_gps",
                    Category = "Location",
                    Confidence = 1.0,
                    Source = Name
                });
            }
            else
            {
                result.Tags.Add(new AgentTag
                {
                    Name = "sin_gps",
                    Category = "Location",
                    Confidence = 1.0,
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

    private string GetSeason(DateTime date)
    {
        int month = date.Month;
        if (month == 12 || month == 1 || month == 2) return "invierno";
        if (month == 3 || month == 4 || month == 5) return "primavera";
        if (month == 6 || month == 7 || month == 8) return "verano";
        return "otoño";
    }

    private string GetCameraBrand(string cameraModel)
    {
        var modelLower = cameraModel.ToLower();
        if (modelLower.Contains("canon")) return "canon";
        if (modelLower.Contains("nikon")) return "nikon";
        if (modelLower.Contains("sony")) return "sony";
        if (modelLower.Contains("fujifilm")) return "fujifilm";
        if (modelLower.Contains("iphone")) return "iphone";
        if (modelLower.Contains("samsung")) return "samsung";
        return "otra";
    }
}