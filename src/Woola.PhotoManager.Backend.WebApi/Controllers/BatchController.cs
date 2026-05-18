using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class BatchController : ControllerBase
{
    private readonly IPhotoRepository _photoRepo;
    private readonly IAlbumRepository _albumRepo;
    private readonly ITagRepository _tagRepo;
    private readonly WoolaDbContext _db;

    public BatchController(
        IPhotoRepository photoRepo,
        IAlbumRepository albumRepo,
        ITagRepository tagRepo,
        WoolaDbContext db)
    {
        _photoRepo = photoRepo;
        _albumRepo = albumRepo;
        _tagRepo = tagRepo;
        _db = db;
    }

    [HttpPost("delete")]
    public async Task<ActionResult> BatchDelete([FromBody] BatchIdsRequest request)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var id in request.Ids)
                await _photoRepo.DeleteAsync(id);
            await tx.CommitAsync();
            return Ok(new { deleted = request.Ids.Count });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpPost("add-to-album")]
    public async Task<ActionResult> BatchAddToAlbum([FromBody] BatchAlbumRequest request)
    {
        foreach (var photoId in request.PhotoIds)
        {
            if (!await _albumRepo.HasPhotoAsync(request.AlbumId, photoId))
                await _albumRepo.AddPhotoAsync(request.AlbumId, photoId);
        }
        return Ok(new { added = request.PhotoIds.Count });
    }

    [HttpPost("tag")]
    public async Task<ActionResult> BatchTag([FromBody] BatchTagRequest request)
    {
        foreach (var photoId in request.PhotoIds)
        {
            foreach (var tagName in request.TagNames)
            {
                var tag = await _tagRepo.GetOrCreateAsync(tagName, "Manual", false);
                await _tagRepo.AddToPhotoAsync(photoId, tag.Id, 1.0, "Manual");
            }
        }
        return Ok(new { tagged = request.PhotoIds.Count * request.TagNames.Count });
    }

    [HttpPost("rate")]
    public async Task<ActionResult> BatchRate([FromBody] BatchRateRequest request)
    {
        await _db.Photos
            .Where(p => request.PhotoIds.Contains(p.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Orientation, request.Rating));
        return Ok(new { rated = request.PhotoIds.Count });
    }
}

public class BatchIdsRequest
{
    public List<int> Ids { get; set; } = new();
}

public class BatchAlbumRequest
{
    public int AlbumId { get; set; }
    public List<int> PhotoIds { get; set; } = new();
}

public class BatchTagRequest
{
    public List<int> PhotoIds { get; set; } = new();
    public List<string> TagNames { get; set; } = new();
}

public class BatchRateRequest
{
    public List<int> PhotoIds { get; set; } = new();
    public int Rating { get; set; }
}
