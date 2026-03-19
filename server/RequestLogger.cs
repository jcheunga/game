using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace CrownroadServer;

public class RequestLogger
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLogger> _logger;

    public RequestLogger(RequestDelegate next, ILogger<RequestLogger> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var path = context.Request.Path;
        var method = context.Request.Method;
        var profileId = context.Request.Headers["X-Convoy-Profile"].FirstOrDefault() ?? "-";

        try
        {
            await _next(context);
            sw.Stop();
            _logger.LogInformation("{Method} {Path} {StatusCode} {ElapsedMs}ms profile={ProfileId}",
                method, path, context.Response.StatusCode, sw.ElapsedMilliseconds, profileId);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "{Method} {Path} 500 {ElapsedMs}ms profile={ProfileId}",
                method, path, sw.ElapsedMilliseconds, profileId);
            throw;
        }
    }
}
