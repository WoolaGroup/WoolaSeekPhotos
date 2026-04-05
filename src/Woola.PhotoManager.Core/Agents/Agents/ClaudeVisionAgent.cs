using System.Text;
using System.Text.Json;
using Woola.PhotoManager.Domain.Entities;

namespace Woola.PhotoManager.Core.Agents.Agents;

/// <summary>
/// IMP-007: Agente de visión usando Claude API.
/// Genera tags ricos de escenas, objetos, emociones y actividades.
/// Requiere ANTHROPIC_API_KEY como variable de entorno.
/// Se desactiva automáticamente si no hay API key.
/// Modelo: claude-haiku-4-5 (rápido y económico para tagging masivo).
/// </summary>
public class ClaudeVisionAgent : IAgent
{
    public string Name => "ClaudeVisionAgent";
    public string Description => "Descripción visual con IA Claude: escenas, emociones, actividades";
    public int Priority => 9;
    public bool IsEnabled { get; set; }

    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly string? _apiKey;

    private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    public ClaudeVisionAgent()
    {
        _apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        IsEnabled = !string.IsNullOrEmpty(_apiKey);
    }

    public bool CanProcess(Photo photo)
    {
        if (!IsEnabled || string.IsNullOrEmpty(_apiKey)) return false;
        if (!File.Exists(photo.Path)) return false;
        var ext = Path.GetExtension(photo.Path);
        return _supportedExtensions.Contains(ext);
    }

    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            // Determinar media type
            var ext = Path.GetExtension(photo.Path).ToLowerInvariant();
            var mediaType = ext == ".png" ? "image/png" : "image/jpeg";

            // Leer imagen como base64
            var imageBytes = await File.ReadAllBytesAsync(photo.Path, cancellationToken);
            var imageData = Convert.ToBase64String(imageBytes);

            // Construir body de la request
            var requestBody = new
            {
                model = "claude-haiku-4-5",
                max_tokens = 300,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = mediaType,
                                    data = imageData
                                }
                            },
                            new
                            {
                                type = "text",
                                text = "Analiza la imagen. Responde SOLO con JSON válido (sin markdown, sin explicaciones): " +
                                       "{\"escenas\":[],\"objetos\":[],\"emociones\":[],\"actividades\":[]}. " +
                                       "Máximo 3 items por array. Items en español, 1-3 palabras cada uno. " +
                                       "Si no aplica una categoría, deja el array vacío."
                            }
                        }
                    }
                }
            };

            var jsonBody = JsonSerializer.Serialize(requestBody);

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://api.anthropic.com/v1/messages");
            httpRequest.Headers.Add("x-api-key", _apiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(httpRequest, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.ErrorMessage = $"Claude API error {(int)response.StatusCode}: {responseText[..Math.Min(200, responseText.Length)]}";
                result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                return result;
            }

            // Parsear respuesta de la API
            using var apiDoc = JsonDocument.Parse(responseText);
            var contentText = apiDoc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            // Limpiar posibles bloques markdown del modelo
            contentText = contentText.Trim();
            if (contentText.StartsWith("```"))
            {
                var firstNewline = contentText.IndexOf('\n');
                var lastBacktick = contentText.LastIndexOf("```");
                if (firstNewline > 0 && lastBacktick > firstNewline)
                    contentText = contentText[(firstNewline + 1)..lastBacktick].Trim();
            }

            using var parsed = JsonDocument.Parse(contentText);

            // Mapeo de categorías en español a nombres de categoría interna
            var categoryMap = new[]
            {
                ("escenas",    "Escena"),
                ("objetos",    "Objeto"),
                ("emociones",  "Emoción"),
                ("actividades","Actividad")
            };

            foreach (var (jsonKey, category) in categoryMap)
            {
                if (!parsed.RootElement.TryGetProperty(jsonKey, out var arr)) continue;
                foreach (var item in arr.EnumerateArray())
                {
                    var val = item.GetString()?.Trim().ToLower();
                    if (!string.IsNullOrEmpty(val))
                        result.Tags.Add(new AgentTag
                        {
                            Name = val,
                            Category = category,
                            Confidence = 0.85,
                            Source = Name
                        });
                }
            }
        }
        catch (JsonException ex)
        {
            // El modelo devolvió JSON inválido — no rompemos la indexación
            result.Success = false;
            result.ErrorMessage = $"JSON parse error: {ex.Message}";
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
