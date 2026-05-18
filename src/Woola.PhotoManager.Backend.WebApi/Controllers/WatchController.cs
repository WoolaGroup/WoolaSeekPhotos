using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.WebApi.Services;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class WatchController : ControllerBase
{
    private readonly WatchFolderService _watchService;

    public WatchController(WatchFolderService watchService) => _watchService = watchService;

    [HttpGet]
    public ActionResult GetStatus()
    {
        return Ok(new
        {
            isWatching = _watchService.IsWatching,
            folderPath = _watchService.FolderPath,
            isConfigured = !string.IsNullOrEmpty(_watchService.FolderPath)
        });
    }

    [HttpPost("start")]
    public ActionResult Start([FromBody] WatchFolderRequest request)
    {
        if (string.IsNullOrEmpty(request.FolderPath) || !Directory.Exists(request.FolderPath))
            return BadRequest("Invalid folder path");

        _watchService.StartWatching(request.FolderPath);
        return Ok(new { status = "watching", folder = request.FolderPath });
    }

    [HttpPost("stop")]
    public ActionResult Stop()
    {
        _watchService.StopWatching();
        return Ok(new { status = "stopped" });
    }
}

public class WatchFolderRequest
{
    public string FolderPath { get; set; } = string.Empty;
}
