using MediatR;
using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.Application.Common.Interfaces;
using Woola.PhotoManager.Backend.Application.Photos.Commands;
using Woola.PhotoManager.Backend.Application.Photos.Queries;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PhotosController : ControllerBase
{
    private readonly IMediator _mediator;

    public PhotosController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<PhotoDto>>> GetPhotos(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? albumId = null,
        [FromQuery] string? tag = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = "dateTaken",
        [FromQuery] string? sortDir = "desc")
    {
        var query = new GetPhotosQuery(page, pageSize, albumId, tag, search, sortBy, sortDir);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PhotoDetailDto>> GetPhoto(int id)
    {
        var result = await _mediator.Send(new GetPhotoDetailQuery(id));
        if (result == null) return NotFound();
        return Ok(result);
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedApiResponse<PhotoDto>>> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
            var query = new GetPhotosQuery(page, pageSize, null, null, q);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("index")]
    public async Task<ActionResult<IndexJobResult>> StartIndexing(
        [FromBody] IndexPhotosCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPost("{id}/analyze")]
    public async Task<ActionResult> AnalyzePhoto(int id,
        [FromServices] IAnalysisService analysisService)
    {
        var result = await analysisService.AnalyzePhotoAsync(id);
        return Ok(result);
    }

    [HttpPost("index/cancel")]
    public async Task<ActionResult> CancelIndexing([FromServices] IIndexingService indexingService)
    {
        await indexingService.CancelIndexingAsync();
        return Ok(new { status = "cancelled" });
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeletePhoto(int id,
        [FromServices] IPhotoRepository repo)
    {
        var photo = await repo.GetByIdAsync(id);
        if (photo == null) return NotFound();

        // Delete thumbnail
        if (!string.IsNullOrEmpty(photo.ThumbnailPath))
        {
            var thumbDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WoolaPhotos", "thumbnails");
            var thumbFile = Path.Combine(thumbDir, photo.ThumbnailPath);
            if (System.IO.File.Exists(thumbFile))
                System.IO.File.Delete(thumbFile);
        }

        // Delete original file
        if (System.IO.File.Exists(photo.Path))
            System.IO.File.Delete(photo.Path);

        await repo.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("geo")]
    public async Task<ActionResult<List<PhotoDto>>> GetGeoPhotos()
    {
        // Placeholder - will be implemented with full service
        return Ok(new List<PhotoDto>());
    }
}
