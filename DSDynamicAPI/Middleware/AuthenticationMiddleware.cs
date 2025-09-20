// =====================================================
// AuthenticationMiddleware - NUEVO (reemplaza TokenValidationMiddleware)
// =====================================================
using System.Net;
using System.Text.Json;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly string[] _excludedPaths = { "/swagger", "/health", "/api/info", "/api/health", "/api/docs" };

    public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuthenticationService authService, IConfigurationService configService)
    {
        // Saltar validación para rutas públicas
        if (ShouldSkipValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        try
        {
            // Extraer IdAPI de la query string
            if (!context.Request.Query.TryGetValue("idApi", out var idApiValue) || !int.TryParse(idApiValue, out var idAPI))
            {
                await WriteErrorResponse(context, HttpStatusCode.BadRequest, "IdAPI requerido");
                return;
            }

            // Obtener el tipo de autenticación requerido por la API
            var requiredAuthType = await authService.GetRequiredAuthTypeAsync(idAPI);

            if (string.IsNullOrEmpty(requiredAuthType))
            {
                await WriteErrorResponse(context, HttpStatusCode.NotFound, "API no encontrada");
                return;
            }

            // Si es API pública (NONE), permitir acceso sin autenticación
            if (requiredAuthType == "NONE")
            {
                await UpdateRequestContext(context, idAPI, null, "NONE");
                await _next(context);
                return;
            }

            // Extraer credencial del request
            var credential = ExtractCredential(context, requiredAuthType);
            if (string.IsNullOrEmpty(credential))
            {
                var authHeaderExample = GetAuthHeaderExample(requiredAuthType);
                await WriteErrorResponse(context, HttpStatusCode.Unauthorized,
                    $"Credencial requerida. Usar header: {authHeaderExample}");
                return;
            }

            // Crear request de validación
            var validationRequest = new CredentialValidationRequest
            {
                IdAPI = idAPI,
                TipoAuth = Enum.Parse<TipoAutenticacion>(requiredAuthType),
                Credential = credential,
                Environment = context.Request.Query["environment"].FirstOrDefault() ?? "PRODUCTION",
                Headers = ExtractHeaders(context),
                IPAddress = GetClientIpAddress(context)
            };

            // Validar autenticación
            var authResult = await authService.AuthenticateAsync(validationRequest);

            if (!authResult.IsValid)
            {
                // Log intento fallido
                await authService.LogAuthenticationAttemptAsync(idAPI, authResult.IdCredencial, false,
                    authResult.ErrorMessage, GetClientIpAddress(context), validationRequest.Environment);

                await WriteErrorResponse(context, HttpStatusCode.Unauthorized,
                    authResult.ErrorMessage ?? "Autenticación fallida");
                return;
            }

            if (authResult.RateLimitExceeded)
            {
                context.Response.Headers.Add("X-RateLimit-Remaining", "0");
                context.Response.Headers.Add("X-RateLimit-Reset",
                    authResult.ResetTime.HasValue ? new DateTimeOffset(authResult.ResetTime.Value).ToUnixTimeSeconds().ToString() :
                    new DateTimeOffset(DateTime.UtcNow.AddMinutes(1)).ToUnixTimeSeconds().ToString());

                await WriteErrorResponse(context, HttpStatusCode.TooManyRequests, "Rate limit excedido");
                return;
            }

            // Log intento exitoso
            await authService.LogAuthenticationAttemptAsync(idAPI, authResult.IdCredencial, true,
                null, GetClientIpAddress(context), validationRequest.Environment);

            // Actualizar contexto del request
            await UpdateRequestContext(context, idAPI, authResult.IdCredencial, authResult.TipoAuth, authResult);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en middleware de autenticación");
            await WriteErrorResponse(context, HttpStatusCode.InternalServerError, "Error interno de autenticación");
        }
    }

    private bool ShouldSkipValidation(PathString path)
    {
        return _excludedPaths.Any(excluded => path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase));
    }

    private string? ExtractCredential(HttpContext context, string authType)
    {
        return authType switch
        {
            "TOKEN" => ExtractFromAuthorizationHeader(context, "Bearer") ?? ExtractFromCustomHeader(context, "X-API-Token"),
            "APIKEY" => ExtractFromCustomHeader(context, "X-API-Key") ?? ExtractFromAuthorizationHeader(context, "ApiKey"),
            "JWT" => ExtractFromAuthorizationHeader(context, "Bearer") ?? ExtractFromCustomHeader(context, "X-JWT-Token"),
            "OAUTH2" => ExtractFromAuthorizationHeader(context, "Bearer"),
            "NTLM" => ExtractFromAuthorizationHeader(context, "NTLM") ?? ExtractFromAuthorizationHeader(context, "Negotiate"),
            "BASIC" => ExtractFromAuthorizationHeader(context, "Basic"),
            _ => null
        };
    }

    private string? ExtractFromAuthorizationHeader(HttpContext context, string scheme)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith($"{scheme} ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader[$"{scheme} ".Length..].Trim();
        }
        return null;
    }

    private string? ExtractFromCustomHeader(HttpContext context, string headerName)
    {
        return context.Request.Headers[headerName].FirstOrDefault();
    }

    private Dictionary<string, string> ExtractHeaders(HttpContext context)
    {
        var headers = new Dictionary<string, string>();

        foreach (var header in context.Request.Headers)
        {
            if (IsRelevantAuthHeader(header.Key))
            {
                headers[header.Key] = header.Value.ToString();
            }
        }

        return headers;
    }

    private bool IsRelevantAuthHeader(string headerName)
    {
        var relevantHeaders = new[] { "Authorization", "X-API-Token", "X-API-Key", "X-JWT-Token", "User-Agent", "X-Forwarded-For" };
        return relevantHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase);
    }

    private string GetAuthHeaderExample(string authType)
    {
        return authType switch
        {
            "TOKEN" => "Authorization: Bearer {token} o X-API-Token: {token}",
            "APIKEY" => "X-API-Key: {apikey} o Authorization: ApiKey {apikey}",
            "JWT" => "Authorization: Bearer {jwt}",
            "OAUTH2" => "Authorization: Bearer {oauth2_token}",
            "NTLM" => "Authorization: NTLM {ntlm_token}",
            "BASIC" => "Authorization: Basic {base64_credentials}",
            _ => "Ver documentación de la API"
        };
    }

    private async Task UpdateRequestContext(HttpContext context, int idAPI, int? idCredencial, string tipoAuth, AuthValidationResult? authResult = null)
    {
        if (context.Items["RequestContext"] is RequestContext requestContext)
        {
            requestContext.IdAPI = idAPI;
            requestContext.IdCredencial = idCredencial;
            requestContext.TipoAuth = tipoAuth;

            if (authResult?.AuthMetadata != null)
            {
                foreach (var metadata in authResult.AuthMetadata)
                {
                    requestContext.Metadata[metadata.Key] = metadata.Value;
                }
            }
        }
    }

    private string? GetClientIpAddress(HttpContext context)
    {
        return context.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
               context.Request.Headers["X-Real-IP"].FirstOrDefault() ??
               context.Connection.RemoteIpAddress?.ToString();
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
            RequestId = requestId,
            Timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}