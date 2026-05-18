using System.Diagnostics;

namespace Woola.PhotoManager.Backend.WebApi.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path;

        await _next(context);

        sw.Stop();
        var status = context.Response.StatusCode;

        if (sw.ElapsedMilliseconds > 500)
            _logger.LogWarning("Slow request: {Method} {Path} → {Status} in {Elapsed}ms",
                method, path, status, sw.ElapsedMilliseconds);
        else
            _logger.LogInformation("{Method} {Path} → {Status} in {Elapsed}ms",
                method, path, status, sw.ElapsedMilliseconds);
    }
}
