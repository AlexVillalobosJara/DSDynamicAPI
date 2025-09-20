using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;
using System.Text.Json;

namespace DynamicAPIs.Controllers;

// =====================================================
// DynamicApiController - CONTROLLER PRINCIPAL ACTUALIZADO
// =====================================================
[ApiController]
[Route("api")]
[Produces("application/json")]
public class DynamicApiController : ControllerBase
{
    private readonly IConfigurationService _configService;
    private readonly ISqlExecutionService _sqlService;
    private readonly IAuditService _auditService;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<DynamicApiController> _logger;

    public DynamicApiController(
        IConfigurationService configService,
        ISqlExecutionService sqlService,
        IAuditService auditService,
        IAuthenticationService authService,
        ILogger<DynamicApiController> logger)
    {
        _configService = configService;
        _sqlService = sqlService;
        _auditService = auditService;
        _authService = authService;
        _logger = logger;
    }


    [HttpGet("execute")]
    [EnableRateLimiting("DynamicAuthPolicy")]
    [ProducesResponseType(typeof(ApiExecutionResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 429)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> ExecuteApi(
        [FromQuery] int idApi,
        [FromQuery] string environment = "PRODUCTION")
    {
        var stopwatch = Stopwatch.StartNew();
        var requestContext = HttpContext.Items["RequestContext"] as RequestContext;
        var success = false;
        string? errorMessage = null;

        try
        {
            _logger.LogInformation("Ejecutando API {IdAPI} en ambiente {Environment}", idApi, environment);

            // Validar ambiente
            if (!IsValidEnvironment(environment))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "INVALID_ENVIRONMENT",
                    Message = "Ambiente debe ser TEST o PRODUCTION",
                    StatusCode = 400,
                    RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
                });
            }

            // NUEVO: Extraer credencial de headers HTTP si no está en requestContext
            string? credential = requestContext?.Credential;

            if (string.IsNullOrEmpty(credential))
            {
                credential = ExtractCredentialFromHeaders();
                _logger.LogDebug("Credencial extraída de headers: {HasCredential}", !string.IsNullOrEmpty(credential));
            }
            else
            {
                _logger.LogDebug("Credencial obtenida del RequestContext: {HasCredential}", !string.IsNullOrEmpty(credential));
            }

            // CORREGIDO: Pasar la credencial extraída al servicio de configuración
            var config = await _configService.GetApiConfigurationAsync(idApi, credential);
            if (config == null)
            {
                // MEJORADO: Logging más específico para debugging
                _logger.LogWarning("API {IdAPI} no encontrada o acceso denegado para credencial: {CredentialPreview}",
                    idApi, MaskCredential(credential));

                return NotFound(new ErrorResponse
                {
                    Error = "API_NOT_FOUND",
                    Message = $"API con ID {idApi} no encontrada o acceso denegado",
                    StatusCode = 404,
                    RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString(),
                    Details = new Dictionary<string, object>
                    {
                        ["apiId"] = idApi,
                        ["hasCredential"] = !string.IsNullOrEmpty(credential),
                        ["credentialSource"] = string.IsNullOrEmpty(requestContext?.Credential) ? "headers" : "middleware"
                    }
                });
            }

            // NUEVO: Log de configuración obtenida
            _logger.LogDebug("Configuración obtenida para API {IdAPI}: {NombreAPI}, Auth: {TipoAuth}",
                idApi, config.NombreAPI, config.TipoAutenticacion);

            // Extraer y validar parámetros
            var parameters = ExtractParameters(HttpContext.Request, config.Parametros);
            var validationResult = ValidateParameters(parameters, config.Parametros);

            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Parámetros inválidos para API {IdAPI}: {ValidationError}", idApi, validationResult.ErrorMessage);

                return BadRequest(new ErrorResponse
                {
                    Error = "INVALID_PARAMETERS",
                    Message = validationResult.ErrorMessage!,
                    StatusCode = 400,
                    RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString(),
                    Details = new Dictionary<string, object>
                    {
                        ["requiredParameters"] = config.Parametros.Where(p => p.EsObligatorio).Select(p => p.NombreParametro),
                        ["providedParameters"] = parameters.Keys,
                        ["missingParameters"] = config.Parametros.Where(p => p.EsObligatorio && !parameters.ContainsKey(p.NombreParametro)).Select(p => p.NombreParametro)
                    }
                });
            }

            // Ejecutar función SQL
            var result = await _sqlService.ExecuteSqlAsync(config, parameters, environment);
            success = true;

            // MEJORADO: Response con más información
            var response = new ApiExecutionResponse
            {
                Success = true,
                Data = result,
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                AuthType = config.TipoAutenticacion,
                CredentialId = requestContext?.IdCredencial,
                Environment = environment,
                Metadata = new Dictionary<string, object>
                {
                    ["apiName"] = config.NombreAPI,
                    ["sqlObject"] = config.ObjetoSQL,
                    ["parameterCount"] = parameters.Count
                }
            };

            _logger.LogInformation("API {IdAPI} ({NombreAPI}) ejecutada exitosamente en {ElapsedMs}ms",
                idApi, config.NombreAPI, stopwatch.ElapsedMilliseconds);

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            _logger.LogWarning("Error de argumentos en API {IdAPI}: {Error}", idApi, ex.Message);

            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_ARGUMENT",
                Message = ex.Message,
                StatusCode = 400,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            errorMessage = ex.Message;
            _logger.LogWarning("Error de operación en API {IdAPI}: {Error}", idApi, ex.Message);

            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_OPERATION",
                Message = ex.Message,
                StatusCode = 400,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
            });
        }
        catch (TimeoutException ex)
        {
            errorMessage = "Timeout en la ejecución de la API";
            _logger.LogError(ex, "Timeout ejecutando API {IdAPI}", idApi);

            return StatusCode(408, new ErrorResponse
            {
                Error = "TIMEOUT",
                Message = "La ejecución de la API excedió el tiempo límite",
                StatusCode = 408,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
            });
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger.LogError(ex, "Error inesperado ejecutando API {IdAPI}", idApi);

            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error interno ejecutando la API",
                StatusCode = 500,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
            });
        }
        finally
        {
            stopwatch.Stop();

            // MEJORADO: Auditoría con información de credencial
            try
            {
                if (requestContext != null)
                {
                    string? auditCredential = requestContext.Credential;
                    if (string.IsNullOrEmpty(auditCredential))
                    {
                        auditCredential = ExtractCredentialFromHeaders();
                    }

                    await _auditService.LogAuditoriaAsync(
                        idApi,
                        requestContext.IdCredencial,
                        environment,
                        JsonSerializer.Serialize(ExtractParameters(HttpContext.Request, new List<ApiParameter>())),
                        success,
                        errorMessage,
                        (int)stopwatch.ElapsedMilliseconds,
                        requestContext.ClientIP);
                }
            }
            catch (Exception auditEx)
            {
                _logger.LogError(auditEx, "Error en auditoría para API {IdAPI}", idApi);
            }
        }
    }

// =====================================================
// MÉTODOS AUXILIARES PARA EXTRACCIÓN DE CREDENCIAL
// =====================================================

/// <summary>
/// Extrae la credencial de los headers HTTP según diferentes formatos de autenticación
/// </summary>
private string? ExtractCredentialFromHeaders()
    {
        try
        {
            // 1. Buscar en Authorization header (Bearer, ApiKey, Basic, etc.)
            var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader))
            {
                // Bearer Token (JWT, OAuth2, Token)
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader["Bearer ".Length..].Trim();
                }

                // API Key con formato "ApiKey {key}"
                if (authHeader.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader["ApiKey ".Length..].Trim();
                }

                // Basic Authentication
                if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader["Basic ".Length..].Trim();
                }

                // NTLM
                if (authHeader.StartsWith("NTLM ", StringComparison.OrdinalIgnoreCase))
                {
                    return authHeader["NTLM ".Length..].Trim();
                }

                // Si no reconoce el formato, retornar el header completo (menos común)
                return authHeader;
            }

            // 2. Buscar en headers personalizados
            var customHeaders = new[]
            {
            "X-API-Key",
            "X-API-Token",
            "X-Auth-Token",
            "X-JWT-Token",
            "ApiKey",
            "Token"
        };

            foreach (var headerName in customHeaders)
            {
                var headerValue = HttpContext.Request.Headers[headerName].FirstOrDefault();
                if (!string.IsNullOrEmpty(headerValue))
                {
                    return headerValue;
                }
            }

            // 3. OPCIONAL: Buscar en query parameters (menos seguro, pero a veces se usa)
            var queryParams = new[] { "token", "apikey", "key", "auth" };
            foreach (var paramName in queryParams)
            {
                var paramValue = HttpContext.Request.Query[paramName].FirstOrDefault();
                if (!string.IsNullOrEmpty(paramValue))
                {
                    _logger.LogWarning("Credencial encontrada en query parameter {ParamName} - no recomendado por seguridad", paramName);
                    return paramValue;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extrayendo credencial de headers");
            return null;
        }
    }

    /// <summary>
    /// Enmascara una credencial para logging seguro
    /// </summary>
    private string? MaskCredential(string? credential)
    {
        if (string.IsNullOrEmpty(credential))
            return "null";

        if (credential.Length <= 8)
            return "****";

        return credential[..4] + "****" + credential[^4..];
    }

    /// <summary>
    /// Obtiene información detallada de los headers de autenticación para debugging
    /// </summary>
    private Dictionary<string, object> GetAuthHeadersInfo()
    {
        var info = new Dictionary<string, object>();

        try
        {
            // Authorization header
            var authHeader = HttpContext.Request.Headers.Authorization.FirstOrDefault();
            info["hasAuthorizationHeader"] = !string.IsNullOrEmpty(authHeader);
            if (!string.IsNullOrEmpty(authHeader))
            {
                var parts = authHeader.Split(' ', 2);
                info["authorizationType"] = parts.Length > 0 ? parts[0] : "unknown";
                info["hasAuthorizationValue"] = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]);
            }

            // Custom headers
            var customHeaders = new[] { "X-API-Key", "X-API-Token", "X-Auth-Token", "X-JWT-Token" };
            foreach (var headerName in customHeaders)
            {
                var value = HttpContext.Request.Headers[headerName].FirstOrDefault();
                info[$"has{headerName.Replace("-", "")}"] = !string.IsNullOrEmpty(value);
            }

            // Query parameters
            var queryParams = new[] { "token", "apikey", "key", "auth" };
            foreach (var paramName in queryParams)
            {
                var value = HttpContext.Request.Query[paramName].FirstOrDefault();
                info[$"hasQuery{paramName}"] = !string.IsNullOrEmpty(value);
            }

        }
        catch (Exception ex)
        {
            info["error"] = ex.Message;
        }

        return info;
    }

    /// <summary>
    /// Obtiene información detallada de una API específica
    /// </summary>
    [HttpGet("info/{idApi}")]
    [ProducesResponseType(typeof(ApiInfo), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> GetApiInfo(int idApi)
    {
        try
        {
            var config = await _configService.GetApiConfigurationAsync(idApi);
            if (config == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "API_NOT_FOUND",
                    Message = $"API con ID {idApi} no encontrada",
                    StatusCode = 404
                });
            }

            var apiInfo = new ApiInfo
            {
                IdAPI = config.IdAPI,
                NombreAPI = config.NombreAPI,
                Descripcion = config.Descripcion,
                TipoObjeto = config.TipoObjeto,
                Parametros = config.Parametros.Select(p => new ParameterInfo
                {
                    Nombre = p.NombreParametro,
                    Tipo = p.TipoParametro,
                    Requerido = p.EsObligatorio,
                    ValorPorDefecto = p.ValorPorDefecto,
                    Descripcion = p.Descripcion
                }).ToList(),
                Endpoint = $"/api/execute?idApi={idApi}",
                ExampleCall = GenerateExampleCall(idApi, config.Parametros)
            };

            return Ok(apiInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo información de API {IdAPI}", idApi);
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error obteniendo información de la API",
                StatusCode = 500
            });
        }
    }

    /// <summary>
    /// Obtiene estadísticas de uso de una API específica
    /// </summary>
    [HttpGet("stats/{idApi}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    public async Task<IActionResult> GetApiStats(int idApi, [FromQuery] DateTime? fechaDesde = null, [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            // Verificar que la API existe
            var config = await _configService.GetApiConfigurationAsync(idApi);
            if (config == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "API_NOT_FOUND",
                    Message = $"API con ID {idApi} no encontrada",
                    StatusCode = 404
                });
            }

            // Obtener estadísticas
            var usageStats = await _auditService.GetUsageStatisticsAsync(idApi, fechaDesde, fechaHasta);
            var authStats = await _authService.GetAuthStatsAsync(idApi);
            var recentActivity = await _authService.GetRecentActivityAsync(idApi, 10);

            var stats = new
            {
                API = new
                {
                    config.IdAPI,
                    config.NombreAPI,
                    TipoAuth = authStats.TipoAuth.ToString(),
                    NombreTipoAuth = authStats.NombreTipoAuth
                },
                Usage = usageStats.FirstOrDefault(),
                Authentication = authStats,
                RecentActivity = recentActivity,
                Period = new
                {
                    From = fechaDesde?.ToString("yyyy-MM-dd") ?? "30 días atrás",
                    To = fechaHasta?.ToString("yyyy-MM-dd") ?? "hoy"
                }
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas de API {IdAPI}", idApi);
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error obteniendo estadísticas de la API",
                StatusCode = 500
            });
        }
    }

    // =====================================================
    // MÉTODOS AUXILIARES
    // =====================================================

    private bool IsValidEnvironment(string environment)
    {
        var validEnvironments = new[] { "TEST", "PRODUCTION" };
        return validEnvironments.Contains(environment.ToUpper());
    }

    private Dictionary<string, object?> ExtractParameters(HttpRequest request, List<ApiParameter> configParams)
    {
        var parameters = new Dictionary<string, object?>();

        // Extraer de query string
        foreach (var query in request.Query)
        {
            if (query.Key != "idApi" && query.Key != "environment")
            {
                parameters[query.Key] = query.Value.ToString();
            }
        }

        // Extraer de form (si es POST con form data)
        if (request.HasFormContentType)
        {
            foreach (var form in request.Form)
            {
                parameters[form.Key] = form.Value.ToString();
            }
        }

        return parameters;
    }

    private (bool IsValid, string? ErrorMessage) ValidateParameters(
        Dictionary<string, object?> providedParams,
        List<ApiParameter> configParams)
    {
        var errors = new List<string>();

        foreach (var configParam in configParams)
        {
            var paramExists = providedParams.ContainsKey(configParam.NombreParametro);
            var paramValue = paramExists ? providedParams[configParam.NombreParametro] : null;

            // Validar parámetros obligatorios
            if (configParam.EsObligatorio && (paramValue == null || string.IsNullOrWhiteSpace(paramValue?.ToString())))
            {
                errors.Add($"Parámetro '{configParam.NombreParametro}' es obligatorio");
                continue;
            }

            // Si no es obligatorio y no está presente, usar valor por defecto
            if (!paramExists && !string.IsNullOrEmpty(configParam.ValorPorDefecto))
            {
                providedParams[configParam.NombreParametro] = configParam.ValorPorDefecto;
                continue;
            }

            // Validar tipo de dato si el valor está presente
            if (paramValue != null && !ValidateParameterType(paramValue.ToString()!, configParam.TipoParametro))
            {
                errors.Add($"Parámetro '{configParam.NombreParametro}' debe ser de tipo {configParam.TipoParametro}");
            }
        }

        return (errors.Count == 0, errors.Count > 0 ? string.Join("; ", errors) : null);
    }

    private bool ValidateParameterType(string value, string expectedType)
    {
        return expectedType.ToUpper() switch
        {
            "INT" or "INTEGER" => int.TryParse(value, out _),
            "BIGINT" => long.TryParse(value, out _),
            "DECIMAL" or "FLOAT" or "DOUBLE" => decimal.TryParse(value, out _),
            "DATETIME" or "DATE" => DateTime.TryParse(value, out _),
            "BIT" or "BOOLEAN" => bool.TryParse(value, out _),
            "STRING" or "NVARCHAR" or "VARCHAR" or "CHAR" => true, // Siempre válido para strings
            _ => true // Para tipos no reconocidos, asumir válido
        };
    }

    private string GenerateExampleCall(int idApi, List<ApiParameter> parameters)
    {
        var baseUrl = "/api/execute";
        var queryParams = new List<string> { $"idApi={idApi}" };

        foreach (var param in parameters.Where(p => p.EsObligatorio).Take(3)) // Máximo 3 para el ejemplo
        {
            var exampleValue = GetExampleValue(param.TipoParametro);
            queryParams.Add($"{param.NombreParametro}={exampleValue}");
        }

        return $"{baseUrl}?{string.Join("&", queryParams)}";
    }

    private string GetExampleValue(string tipo)
    {
        return tipo.ToUpper() switch
        {
            "STRING" or "NVARCHAR" or "VARCHAR" or "CHAR" => "ejemplo",
            "INT" or "INTEGER" or "BIGINT" => "123",
            "DECIMAL" or "FLOAT" or "DOUBLE" => "123.45",
            "DATETIME" or "DATE" => DateTime.Now.ToString("yyyy-MM-dd"),
            "BIT" or "BOOLEAN" => "true",
            _ => "valor"
        };
    }
}