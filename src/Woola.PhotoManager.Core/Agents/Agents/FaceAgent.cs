using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Agents.Agents;

public class FaceAgent : IAgent
{
    private readonly IFaceService       _faceService;
    private readonly FaceRepository     _faceRepository;
    private readonly TagRepository      _tagRepository;
    private readonly ILogger<FaceAgent> _logger;

    public string Name        => "FaceAgent";
    public string Description => "Detecta y reconoce rostros en imágenes";
    public int    Priority    => 5;
    public bool   IsEnabled   { get; set; } = true;

    public FaceAgent(IFaceService faceService, FaceRepository faceRepository,
                     TagRepository tagRepository,
                     ILogger<FaceAgent>? logger = null)
    {
        _faceService    = faceService;
        _faceRepository = faceRepository;
        _tagRepository  = tagRepository;
        _logger         = logger ?? NullLogger<FaceAgent>.Instance;
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
        var result    = new AgentResult { AgentName = Name, Success = true };

        try
        {
            await _faceService.DownloadModelsIfNeededAsync();

            var faces = await _faceService.DetectFacesAsync(photo.Path);

            if (faces.Count == 0)
            {
                result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
                return result;
            }

            // Guardar cada rostro en DB y generar su embedding
            foreach (var detected in faces)
            {
                if (cancellationToken.IsCancellationRequested) break;

                float[]? embedding = null;
                try
                {
                    embedding = await _faceService.GenerateEmbeddingAsync(photo.Path, detected);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("[FaceAgent] Embedding falló para foto {PhotoId}: {Msg}",
                        photo.Id, ex.Message);
                    // guardamos igualmente el bounding box sin embedding
                }

                await _faceRepository.InsertFaceAsync(new Face
                {
                    PhotoId         = photo.Id,
                    X               = detected.X,
                    Y               = detected.Y,
                    Width           = detected.Width,
                    Height          = detected.Height,
                    Confidence      = detected.Confidence,
                    Encoding        = embedding != null ? SerializeEmbedding(embedding) : null,
                    IsUserConfirmed = false
                });
            }

            // Tags de presencia y cantidad de rostros
            result.Tags.Add(new AgentTag
            {
                Name       = "con_rostros",
                Category   = "Feature",
                Confidence = 1.0,
                Source     = Name
            });

            if (faces.Count >= 2)
            {
                result.Tags.Add(new AgentTag
                {
                    Name       = $"rostros_{faces.Count}",
                    Category   = "Feature",
                    Confidence = 1.0,
                    Source     = Name
                });
            }

            result.ProcessingTimeMs = (DateTime.Now - startTime).TotalMilliseconds;
            _logger.LogDebug("[FaceAgent] {Faces} rostros en foto {PhotoId} ({Ms:F0}ms)",
                faces.Count, photo.Id, result.ProcessingTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[FaceAgent] Error en foto {PhotoId}: {Msg}", photo.Id, ex.Message);
            result.Success      = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private static byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * 4];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
