using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.Domain.Repositories;
using Woola.PhotoManager.Shared.Models;
using CoreEvent = Woola.PhotoManager.Core.Services;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class EventsController : ControllerBase
{
    private readonly CoreEvent.IEventDetectionService _eventDetection;
    private readonly IPhotoRepository _photoRepo;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        CoreEvent.IEventDetectionService eventDetection,
        IPhotoRepository photoRepo,
        ILogger<EventsController> logger)
    {
        _eventDetection = eventDetection;
        _photoRepo = photoRepo;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<EventDto>>> GetAll()
    {
        try
        {
            var events = await _eventDetection.DetectEventsAsync();
            return Ok(events.Select(e => new EventDto
            {
                Name = e.Name,
                Start = e.Start,
                End = e.End,
                PhotoCount = e.PhotoCount
            }).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting events");
            return Ok(new List<EventDto>());
        }
    }

    [HttpGet("{id}/photos")]
    public async Task<ActionResult<PagedApiResponse<PhotoDto>>> GetPhotos(
        string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!DateTime.TryParse(id, out var eventDate))
            return BadRequest("Invalid event date. Use ISO format (yyyy-MM-dd).");

        var start = eventDate.Date;
        var end = start.AddDays(1);

        var photos = await _photoRepo.GetByDateRangeAsync(start, end);
        var total = photos.Count;
        var paged = photos
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PhotoDto
            {
                Id = p.Id,
                FileName = p.FileName,
                Path = p.Path,
                FileSize = p.FileSize,
                DateTaken = p.DateTaken,
                Width = p.Width,
                Height = p.Height,
                Status = p.Status,
                ThumbnailUrl = p.ThumbnailPath != null ? $"/thumbnails/{p.ThumbnailPath}" : null,
                CameraModel = p.CameraModel,
                CreatedAt = p.CreatedAt
            }).ToList();

        return Ok(PagedApiResponse<PhotoDto>.Ok(paged, total, page, pageSize));
    }
}

public class EventDto
{
    public string Name { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int PhotoCount { get; set; }
    public string Id => Start.ToString("yyyy-MM-dd");
    public string DateRange => Start == End
        ? Start.ToString("d MMM yyyy")
        : $"{Start:d MMM} - {End:d MMM yyyy}";
}
