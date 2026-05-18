using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class FacesController : ControllerBase
{
    private readonly IFaceRepository _faceRepo;

    public FacesController(IFaceRepository faceRepo) => _faceRepo = faceRepo;

    [HttpGet]
    public async Task<ActionResult<List<FaceGroupDto>>> GetAll()
    {
        var summary = await _faceRepo.GetPersonSummaryAsync();
        var dtos = new List<FaceGroupDto>();

        foreach (var (name, count) in summary)
        {
            dtos.Add(new FaceGroupDto
            {
                PersonName = name,
                FaceCount = count,
                PhotoCount = count
            });
        }

        return Ok(dtos);
    }

    [HttpPut("{id}/name")]
    public async Task<ActionResult> RenameFace(int id, [FromBody] RenameFaceRequest request)
    {
        await _faceRepo.UpdatePersonAsync(id, request.PersonName, null);
        return NoContent();
    }

    [HttpGet("person/{personName}/photos")]
    public async Task<ActionResult<PagedApiResponse<PhotoDto>>> GetPersonPhotos(
        string personName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await _faceRepo.GetPhotosByPersonAsync(personName, page, pageSize);
        return Ok(PagedApiResponse<PhotoDto>.Ok(
            result.Items.Select(p => new PhotoDto
            {
                Id = p.Id,
                FileName = p.FileName,
                Path = p.Path,
                Hash = p.Hash,
                FileSize = p.FileSize,
                DateTaken = p.DateTaken,
                Width = p.Width,
                Height = p.Height,
                Status = p.Status,
                ThumbnailUrl = p.ThumbnailPath != null ? $"/thumbnails/{p.ThumbnailPath}" : null,
                CreatedAt = p.CreatedAt
            }).ToList(),
            result.TotalCount, page, pageSize));
    }
}
