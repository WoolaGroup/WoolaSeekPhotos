using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.Infrastructure.Data;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class HealthController : ControllerBase
{
    private readonly WoolaDbContext _db;

    public HealthController(WoolaDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult> Get()
    {
        var dbConnected = false;
        try
        {
            dbConnected = await _db.Database.CanConnectAsync();
        }
        catch { /* ignore */ }

        return Ok(new
        {
            Status = dbConnected ? "healthy" : "degraded",
            Database = dbConnected ? "connected" : "disconnected",
            Timestamp = DateTime.UtcNow
        });
    }
}
