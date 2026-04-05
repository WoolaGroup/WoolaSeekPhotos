using System.Text.Json;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

/// <summary>
/// IMP-006: Reverse geocoding GPS → tags de ubicación (ciudad, estado, país).
/// Usa Nominatim (OpenStreetMap), gratuito, máx 1 req/s.
/// Cache interno keyed por lat/lon a 2 decimales (~1 km de precisión).
/// </summary>
public class GeoLocationAgent : IAgent
{
    public string Name => "GeoLocationAgent";
    public string Description => "Reverse geocoding GPS → tags ciudad/estado/país (Nominatim)";
    public int Priority => 8;
    public bool IsEnabled { get; set; } = true;

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    // Cache: key = "lat2,lon2", value = JSON string o null si falló
    private readonly Dictionary<string, string?> _cache = new();
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);

    public bool CanProcess(Photo photo)
        => photo.Latitude.HasValue && photo.Longitude.HasValue;

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            var key = $"{photo.Latitude:F2},{photo.Longitude:F2}";
            string? jsonResponse;

            if (!_cache.TryGetValue(key, out jsonResponse))
            {
                await _rateLimiter.WaitAsync(cancellationToken);
                try
                {
                    // Respetar rate limit de Nominatim: 1 req/s
                    await Task.Delay(1100, cancellationToken);

                    var url = $"https://nominatim.openstreetmap.org/reverse" +
                              $"?lat={photo.Latitude:F6}&lon={photo.Longitude:F6}" +
                              $"&format=json&accept-language=es";

                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.UserAgent.ParseAdd("WoolaPhotos/1.0 (photo-manager)");

                    var response = await _http.SendAsync(request, cancellationToken);
                    jsonResponse = response.IsSuccessStatusCode
                        ? await response.Content.ReadAsStringAsync(cancellationToken)
                        : null;

                    _cache[key] = jsonResponse;
                }
                catch (Exception ex)
                {
                    _cache[key] = null;
                    result.Success = false;
                    result.ErrorMessage = $"Nominatim error: {ex.Message}";
                    result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                    return result;
                }
                finally
                {
                    _rateLimiter.Release();
                }
            }

            if (string.IsNullOrEmpty(jsonResponse))
            {
                result.Success = false;
                result.ErrorMessage = "Sin respuesta de Nominatim";
                result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                return result;
            }

            // Parsear y extraer tags de ubicación
            using var doc = JsonDocument.Parse(jsonResponse);
            if (!doc.RootElement.TryGetProperty("address", out var address))
            {
                result.Success = false;
                result.ErrorMessage = "Respuesta Nominatim sin campo 'address'";
                result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                return result;
            }

            // Ciudad (city, town, village, hamlet en orden de preferencia)
            foreach (var cityField in new[] { "city", "town", "village", "hamlet", "municipality" })
            {
                if (address.TryGetProperty(cityField, out var cityVal))
                {
                    var cityName = cityVal.GetString();
                    if (!string.IsNullOrEmpty(cityName))
                    {
                        result.Tags.Add(new AgentTag
                        {
                            Name = $"ciudad:{cityName}",
                            Category = "Ubicación",
                            Confidence = 0.90,
                            Source = Name
                        });
                        break;
                    }
                }
            }

            // Estado/provincia
            if (address.TryGetProperty("state", out var stateVal))
            {
                var stateName = stateVal.GetString();
                if (!string.IsNullOrEmpty(stateName))
                    result.Tags.Add(new AgentTag
                    {
                        Name = $"estado:{stateName}",
                        Category = "Ubicación",
                        Confidence = 0.90,
                        Source = Name
                    });
            }

            // País
            if (address.TryGetProperty("country", out var countryVal))
            {
                var countryName = countryVal.GetString();
                if (!string.IsNullOrEmpty(countryName))
                    result.Tags.Add(new AgentTag
                    {
                        Name = $"país:{countryName}",
                        Category = "Ubicación",
                        Confidence = 0.95,
                        Source = Name
                    });
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        return result;
    }
}
