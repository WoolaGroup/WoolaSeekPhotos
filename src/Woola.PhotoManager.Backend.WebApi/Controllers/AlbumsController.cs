using MediatR;
using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.Application.Albums.Commands;
using Woola.PhotoManager.Backend.Application.Albums.Queries;
using Woola.PhotoManager.Backend.Application.Photos.Queries;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AlbumsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAlbumRepository _albumRepo;

    public AlbumsController(IMediator mediator, IAlbumRepository albumRepo)
    {
        _mediator = mediator;
        _albumRepo = albumRepo;
    }

    [HttpGet]
    public async Task<ActionResult<List<AlbumDto>>> GetAll()
    {
        var result = await _mediator.Send(new GetAlbumsQuery());
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<AlbumDto>> GetById(int id)
    {
        var album = await _albumRepo.GetByIdAsync(id);
        if (album == null) return NotFound();

        return Ok(new AlbumDto
        {
            Id = album.Id,
            Name = album.Name,
            Description = album.Description,
            CoverPhotoId = album.CoverPhotoId,
            PhotoCount = album.AlbumPhotos?.Count ?? 0,
            CreatedAt = album.CreatedAt
        });
    }

    [HttpPost]
    public async Task<ActionResult<AlbumDto>> Create([FromBody] CreateAlbumCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAll), new { id = result.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateAlbumRequest request)
    {
        var album = await _albumRepo.GetByIdAsync(id);
        if (album == null) return NotFound();

        album.Update(request.Name, request.Description);
        await _albumRepo.UpdateAsync(album);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        await _albumRepo.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{id}/photos")]
    public async Task<ActionResult<PagedApiResponse<PhotoDto>>> GetPhotos(
        int id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new GetPhotosQuery(page, pageSize, id, null, null, null, null);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("{id}/photos")]
    public async Task<ActionResult> AddPhotos(int id, [FromBody] AddPhotosToAlbumRequest request)
    {
        await _mediator.Send(new AddPhotosToAlbumCommand(id, request.PhotoIds));
        return NoContent();
    }

    [HttpDelete("{id}/photos/{photoId}")]
    public async Task<ActionResult> RemovePhoto(int id, int photoId)
    {
        await _albumRepo.RemovePhotoAsync(id, photoId);
        return NoContent();
    }
}
