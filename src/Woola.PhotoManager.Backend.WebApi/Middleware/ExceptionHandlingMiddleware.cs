using System.Net;
using System.Text.Json;
using Woola.PhotoManager.Shared.Models;

namespace Woola.PhotoManager.Backend.WebApi.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = ErrorResponseFactory.Create(ex, context.TraceIdentifier);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}

public static class ErrorResponseFactory
{
    public static ErrorResponse Create(Exception ex, string? traceId) => new()
    {
        Type = ex.GetType().Name,
        Title = "An error occurred",
        Status = 500,
        Detail = ex.Message,
        TraceId = traceId
    };
}
