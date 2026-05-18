using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SimilarController : ControllerBase
{
    private readonly IPhotoRepository _photoRepo;
    private readonly ITagRepository _tagRepo;

    public SimilarController(IPhotoRepository photoRepo, ITagRepository tagRepo)
    {
        _photoRepo = photoRepo;
        _tagRepo = tagRepo;
    }

    [HttpGet("{photoId}")]
    public async Task<ActionResult<List<SimilarPhotoDto>>> GetSimilar(int photoId)
    {
        var photo = await _photoRepo.GetByIdAsync(photoId);
        if (photo == null) return NotFound();

        var tags = await _tagRepo.GetForPhotoAsync(photoId);
        var tagNames = tags.Select(t => t.Name).ToList();

        if (tagNames.Count == 0)
            return Ok(new List<SimilarPhotoDto>());

        // Find photos sharing tags, ordered by overlap count
        var allPhotos = await _photoRepo.GetPhotosAsync(1, 500, null, null);
        var similar = new List<SimilarPhotoDto>();

        foreach (var p in allPhotos.Items.Where(p => p.Id != photoId))
        {
            var otherTags = await _tagRepo.GetForPhotoAsync(p.Id);
            var common = tagNames.Intersect(otherTags.Select(t => t.Name)).Count();
            if (common > 0)
            {
                var maxTags = Math.Max(tagNames.Count, otherTags.Count);
                var similarity = maxTags > 0 ? (double)common / maxTags : 0;
                similar.Add(new SimilarPhotoDto
                {
                    Photo = new PhotoDto
                    {
                        Id = p.Id,
                        FileName = p.FileName,
                        Path = p.Path,
                        ThumbnailUrl = p.ThumbnailPath != null ? $"/thumbnails/{p.ThumbnailPath}" : null,
                        DateTaken = p.DateTaken,
                        FileSize = p.FileSize
                    },
                    Similarity = similarity,
                    CommonTags = common
                });
            }
        }

        return Ok(similar
            .OrderByDescending(s => s.Similarity)
            .Take(20)
            .ToList());
    }
}

public class SimilarPhotoDto
{
    public PhotoDto Photo { get; set; } = new();
    public double Similarity { get; set; }
    public int CommonTags { get; set; }
    public string SimilarityPercent => $"{Similarity:P0}";
}
