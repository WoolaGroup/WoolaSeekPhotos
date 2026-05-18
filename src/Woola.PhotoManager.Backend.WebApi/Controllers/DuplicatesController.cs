using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DuplicatesController : ControllerBase
{
    private readonly IPhotoRepository _photoRepo;

    public DuplicatesController(IPhotoRepository photoRepo) => _photoRepo = photoRepo;

    [HttpGet]
    public async Task<ActionResult<List<DuplicateGroupDto>>> GetAll()
    {
        // For now, group by Hash to find exact duplicates
        var photos = await _photoRepo.GetPhotosAsync(1, 10000, null, null);
        var groups = photos.Items
            .GroupBy(p => p.Hash)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateGroupDto
            {
                Hash = g.Key,
                Photos = g.Select(p => new PhotoDto
                {
                    Id = p.Id,
                    FileName = p.FileName,
                    Path = p.Path,
                    FileSize = p.FileSize,
                    ThumbnailUrl = p.ThumbnailPath != null ? $"/thumbnails/{p.ThumbnailPath}" : null,
                    DateTaken = p.DateTaken,
                    CreatedAt = p.CreatedAt
                }).ToList()
            })
            .ToList();

        return Ok(groups);
    }
}

public class DuplicateGroupDto
{
    public string Hash { get; set; } = string.Empty;
    public List<PhotoDto> Photos { get; set; } = new();
}
