// Middleware para validación de tokens
using System.Net;
using System.Text.Json;

public class TokenValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenValidationMiddleware> _logger;
    private readonly string[] _excludedPaths = { "/swagger", "/health", "/api/info", "/api/health" };

    public TokenValidationMiddleware(RequestDelegate next, ILogger<TokenValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITokenValidationService tokenService)
    {
        // Saltar validación para rutas públicas
        if (ShouldSkipValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var token = ExtractToken(context);
        if (string.IsNullOrEmpty(token))
        {
            await WriteErrorResponse(context, HttpStatusCode.Unauthorized, "Token requerido");
            return;
        }

        try
        {
            var validationResult = await tokenService.ValidateTokenAsync(token);

            if (!validationResult.IsValid)
            {
                await WriteErrorResponse(context, HttpStatusCode.Unauthorized,
                    validationResult.ErrorMessage ?? "Token inválido");
                return;
            }

            if (validationResult.RateLimitExceeded)
            {
                context.Response.Headers.Add("X-RateLimit-Remaining", "0");
                context.Response.Headers.Add("X-RateLimit-Reset", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString());

                await WriteErrorResponse(context, HttpStatusCode.TooManyRequests, "Rate limit excedido");
                return;
            }

            // Agregar información del token al contexto
            if (context.Items["RequestContext"] is RequestContext requestContext)
            {
                requestContext.Credential = token;
                requestContext.IdAPI = validationResult.IdAPI;
                requestContext.IdCredencial = validationResult.IdToken;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando token");
            await WriteErrorResponse(context, HttpStatusCode.InternalServerError, "Error interno validando token");
        }
    }

    private bool ShouldSkipValidation(PathString path)
    {
        return _excludedPaths.Any(excluded => path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase));
    }

    private string? ExtractToken(HttpContext context)
    {
        // Buscar en header Authorization (Bearer token)
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["Bearer ".Length..].Trim();
        }

        // Buscar en header X-API-Token
        return context.Request.Headers["X-API-Token"].FirstOrDefault();
    }

    private async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var requestId = context.Items["RequestContext"] is RequestContext ctx ? ctx.RequestId : Guid.NewGuid().ToString();

        var errorResponse = new ErrorResponse
        {
            Error = statusCode.ToString(),
            Message = message,
            StatusCode = (int)statusCode,
            RequestId = requestId
        };

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}