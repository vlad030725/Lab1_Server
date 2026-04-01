using System.Diagnostics;

namespace WebAPI.Middleware;

public sealed class RequestLoggingMiddleware
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
        var stopwatch = Stopwatch.StartNew();
        string method = context.Request.Method;
        string path = context.Request.Path;
        string queryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value! : string.Empty;
        string remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        try
        {
            await _next(context);
            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path}{QueryString} responded {StatusCode} in {ElapsedMilliseconds:0.000} ms from {RemoteIp}",
                method,
                path,
                queryString,
                context.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds,
                remoteIp);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "HTTP {Method} {Path}{QueryString} failed in {ElapsedMilliseconds:0.000} ms from {RemoteIp}",
                method,
                path,
                queryString,
                stopwatch.Elapsed.TotalMilliseconds,
                remoteIp);
            throw;
        }
    }
}
