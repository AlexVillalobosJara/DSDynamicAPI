// Middleware para logging de requests
using System.Diagnostics;

// Middleware para logging de requests
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
        var requestId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        // Agregar información al contexto
        var requestContext = new RequestContext
        {
            RequestId = requestId,
            StartTime = DateTime.UtcNow,
            ClientIP = GetClientIpAddress(context),
            Environment = context.Request.Query["environment"].FirstOrDefault() ?? "PRODUCTION"
        };

        context.Items["RequestContext"] = requestContext;
        context.Response.Headers.Add("X-Request-ID", requestId);

        // Log del request inicial
        _logger.LogInformation("Request started: {RequestId} {Method} {Path} from {ClientIP}",
            requestId, context.Request.Method, context.Request.Path, requestContext.ClientIP);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            _logger.LogInformation("Request completed: {RequestId} {StatusCode} in {ElapsedMs}ms",
                requestId, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
    }

    private string? GetClientIpAddress(HttpContext context)
    {
        return context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
               context.Request.Headers["X-Real-IP"].FirstOrDefault() ??
               context.Connection.RemoteIpAddress?.ToString();
    }
}