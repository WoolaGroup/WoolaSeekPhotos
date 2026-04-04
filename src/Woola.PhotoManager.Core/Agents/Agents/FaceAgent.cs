using Woola.PhotoManager.Common.Services;
using Woola.PhotoManager.Domain.Entities;
using Woola.PhotoManager.Infrastructure.Repositories;

namespace Woola.PhotoManager.Core.Agents.Agents;

public class FaceAgent : IAgent
{
    private readonly IFaceService _faceService;
    private readonly FaceRepository _faceRepository;
    private readonly TagRepository _tagRepository;

    public string Name => "FaceAgent";
    public string Description => "Detecta y reconoce rostros en imágenes";
    public int Priority => 5;
    public bool IsEnabled { get; set; } = true;

    public FaceAgent(IFaceService faceService, FaceRepository faceRepository, TagRepository tagRepository)
    {
        _faceService = faceService;
        _faceRepository = faceRepository;
        _tagRepository = tagRepository;
    }

    public bool CanProcess(Photo photo)
    {
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
        var ext = Path.GetExtension(photo.Path).ToLower();
        return extensions.Contains(ext) && File.Exists(photo.Path);
    }



    public async Task<AgentResult> ExecuteAsync(Photo photo, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[FaceAgent] Procesando foto ID: {photo.Id}");

        var startTime = DateTime.Now;
        var result = new AgentResult { AgentName = Name, Success = true };

        try
        {
            var faces = await _faceService.DetectFacesAsync(photo.Path);
            Console.WriteLine($"[FaceAgent] Foto {photo.Id}: {faces.Count} rostros detectados");

            // Resto del código...
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FaceAgent] Error: {ex.Message}");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * 4];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}