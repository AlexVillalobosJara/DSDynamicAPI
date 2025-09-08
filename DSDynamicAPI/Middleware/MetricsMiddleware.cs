// Middleware para métricas y monitoreo
using System.Diagnostics;

public class MetricsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MetricsMiddleware> _logger;

    public MetricsMiddleware(RequestDelegate next, ILogger<MetricsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        await _next(context);

        stopwatch.Stop();

        // Log de métricas
        if (context.Items["RequestContext"] is RequestContext requestContext && requestContext.IdAPI.HasValue)
        {
            _logger.LogInformation("API Execution Metrics: IdAPI={IdAPI}, Duration={DurationMs}, Status={StatusCode}, Success={Success}",
                requestContext.IdAPI.Value,
                stopwatch.ElapsedMilliseconds,
                context.Response.StatusCode,
                context.Response.StatusCode >= 200 && context.Response.StatusCode < 400);
        }
    }
}