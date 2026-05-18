using Microsoft.AspNetCore.Mvc;
using Woola.PhotoManager.Backend.Application.Common.Interfaces;

namespace Woola.PhotoManager.Backend.WebApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IAgentOrchestrator _agentOrchestrator;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        IAgentOrchestrator agentOrchestrator,
        ILogger<SettingsController> logger)
    {
        _agentOrchestrator = agentOrchestrator;
        _logger = logger;
    }

    [HttpGet("agents")]
    public ActionResult<Dictionary<string, bool>> GetAgentStates()
    {
        return Ok(_agentOrchestrator.GetAgentStates());
    }

    [HttpPut("agents/{agentName}")]
    public ActionResult SetAgentState(string agentName, [FromBody] AgentStateRequest request)
    {
        _agentOrchestrator.SetAgentEnabled(agentName, request.Enabled);
        _logger.LogInformation("Agent {Agent} set to {Enabled}", agentName, request.Enabled);
        return NoContent();
    }

    [HttpGet("info")]
    public ActionResult GetInfo()
    {
        return Ok(new
        {
            Version = "1.0.0",
            Mode = "Local",
            Database = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WoolaPhotos", "photos.db")
        });
    }
}

public class AgentStateRequest
{
    public bool Enabled { get; set; }
}
