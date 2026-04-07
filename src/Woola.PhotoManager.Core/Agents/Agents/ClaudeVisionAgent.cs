using System.Net;
using System.Text;
using System.Text.Json;
using Woola.PhotoManager.Core.Services;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

/// <summary>
/// A1: Agente de visión con Claude API.
/// Priority 4 (entre VisionAgent=3 y FaceAgent=5).
/// API Key: AppSettings.AnthropicApiKey → var entorno ANTHROPIC_API_KEY.
/// Concurrencia limitada a 1 llamada global para no saturar la API.
/// Retry automático en HTTP 429 (rate limit).
/// </summary>
public class ClaudeVisionAgent : IAgent
{
    public string Name        => "ClaudeVisionAgent";
    public string Description => "Descripción visual con IA Claude: escenas, emociones, actividades";
    public int    Priority    => 4;
    public bool   IsEnabled   { get; set; }

    private readonly ISettingsService _settingsService;

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    // Limitar a 1 llamada simultánea a la API de Anthropic
    private static readonly SemaphoreSlim _apiSemaphore = new(1, 1);

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp"
    };

    public ClaudeVisionAgent(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        // Habilitar si hay API key en settings o en variable de entorno
        var apiKey = ResolveApiKey();
        IsEnabled = !string.IsNullOrEmpty(apiKey);
    }

    public bool CanProcess(Photo photo)
    {
        if (!IsEnabled) return false;
        if (!File.Exists(photo.Path)) return false;
        var ext = Path.GetExtension(photo.Path);
        return _supportedExtensions.Contains(ext);
    }

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        // Resolver API key en tiempo de ejecución (funciona aunque se configure después del arranque)
        var apiKey = ResolveApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("[ClaudeVisionAgent] No hay API key configurada — saltando.");
            result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
            return result;
        }

        try
        {
            var ext       = Path.GetExtension(photo.Path).ToLowerInvariant();
            var mediaType = ext == ".png" ? "image/png" : "image/jpeg";

            var imageBytes = await File.ReadAllBytesAsync(photo.Path, cancellationToken);
            var imageData  = Convert.ToBase64String(imageBytes);

            var requestBody = new
            {
                model      = "claude-haiku-4-5",
                max_tokens = 400,
                messages   = new[]
                {
                    new
                    {
                        role    = "user",
                        content = new object[]
                        {
                            new
                            {
                                type   = "image",
                                source = new
                                {
                                    type       = "base64",
                                    media_type = mediaType,
                                    data       = imageData
                                }
                            },
                            new
                            {
                                type = "text",
                                text = "Analiza la imagen. Responde SOLO con JSON válido (sin markdown, sin explicaciones): " +
                                       "{\"escenas\":[],\"objetos\":[],\"emociones\":[],\"actividades\":[]}. " +
                                       "Máximo 5 items por array. Items en español, 1-3 palabras cada uno. " +
                                       "Si no aplica una categoría, deja el array vacío."
                            }
                        }
                    }
                }
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);

            // Concurrencia controlada: 1 llamada a la vez
            await _apiSemaphore.WaitAsync(cancellationToken);
            try
            {
                var response = await SendRequestAsync(apiKey, jsonBody, cancellationToken);

                // Retry una vez en rate-limit
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    Console.WriteLine("[ClaudeVisionAgent] 429 rate-limit, esperando 2s y reintentando...");
                    response.Dispose();
                    await Task.Delay(2000, cancellationToken);
                    response = await SendRequestAsync(apiKey, jsonBody, cancellationToken);
                }

                using (response)
                {
                    var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        var snippet = responseText[..Math.Min(200, responseText.Length)];
                        Console.WriteLine($"[ClaudeVisionAgent] Error {(int)response.StatusCode}: {snippet}");

                        // 4xx cliente → no reintentar
                        result.Success      = false;
                        result.ErrorMessage = $"API error {(int)response.StatusCode}";
                        result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                        return result;
                    }

                    // Parsear respuesta
                    using var apiDoc    = JsonDocument.Parse(responseText);
                    var contentText     = apiDoc.RootElement
                        .GetProperty("content")[0]
                        .GetProperty("text")
                        .GetString() ?? "{}";

                    // Limpiar bloques markdown opcionales
                    contentText = contentText.Trim();
                    if (contentText.StartsWith("```"))
                    {
                        var firstNewline = contentText.IndexOf('\n');
                        var lastBacktick = contentText.LastIndexOf("```");
                        if (firstNewline > 0 && lastBacktick > firstNewline)
                            contentText = contentText[(firstNewline + 1)..lastBacktick].Trim();
                    }

                    using var parsed = JsonDocument.Parse(contentText);
                    var categoryMap  = new[]
                    {
                        ("escenas",     "Escena"),
                        ("objetos",     "Objeto"),
                        ("emociones",   "Emoción"),
                        ("actividades", "Actividad")
                    };

                    foreach (var (jsonKey, category) in categoryMap)
                    {
                        if (!parsed.RootElement.TryGetProperty(jsonKey, out var arr)) continue;
                        foreach (var item in arr.EnumerateArray())
                        {
                            var val = item.GetString()?.Trim().ToLower().Replace(' ', '_');
                            if (!string.IsNullOrEmpty(val))
                                result.Tags.Add(new AgentTag
                                {
                                    Name       = val,
                                    Category   = category,
                                    Confidence = 0.75,
                                    Source     = Name
                                });
                        }
                    }
                }
            }
            finally
            {
                _apiSemaphore.Release();
            }
        }
        catch (OperationCanceledException ex)
        {
            // TaskCanceledException es subclase de OperationCanceledException.
            // Si el token externo está cancelado → propagar. Si no → es un timeout interno.
            if (cancellationToken.IsCancellationRequested) throw;
            Console.WriteLine($"[ClaudeVisionAgent] Timeout: {ex.Message}");
            result.Success      = false;
            result.ErrorMessage = "Timeout";
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[ClaudeVisionAgent] HttpRequestException: {ex.Message}");
            result.Success      = false;
            result.ErrorMessage = ex.Message;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[ClaudeVisionAgent] JSON parse error: {ex.Message}");
            result.Success      = false;
            result.ErrorMessage = $"JSON parse error: {ex.Message}";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClaudeVisionAgent] Unexpected error: {ex.Message}");
            result.Success      = false;
            result.ErrorMessage = ex.Message;
        }

        result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
        return result;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resuelve la API key: settings primero, variable de entorno como fallback.
    /// Se llama en cada ExecuteAsync para soportar claves configuradas post-arranque.
    /// </summary>
    private string? ResolveApiKey()
    {
        var settings = _settingsService.Load();
        return !string.IsNullOrWhiteSpace(settings.AnthropicApiKey)
            ? settings.AnthropicApiKey
            : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }

    private static async Task<HttpResponseMessage> SendRequestAsync(
        string apiKey, string jsonBody, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return await _http.SendAsync(req, ct);
    }
}
