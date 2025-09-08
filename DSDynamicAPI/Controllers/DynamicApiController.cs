using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;
using System.Text.Json;

[ApiController]
[Route("api")]
public class DynamicApiController : ControllerBase
{
    private readonly IConfigurationService _configService;
    private readonly ISqlExecutionService _sqlService;
    private readonly IAuditService _auditService;
    private readonly ILogger<DynamicApiController> _logger;

    public DynamicApiController(
        IConfigurationService configService,
        ISqlExecutionService sqlService,
        IAuditService auditService,
        ILogger<DynamicApiController> logger)
    {
        _configService = configService;
        _sqlService = sqlService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta una API dinámica basada en su configuración
    /// </summary>
    /// <param name="idApi">ID de la API a ejecutar</param>
    /// <param name="environment">Ambiente (TEST/PRODUCTION)</param>
    /// <returns>Resultado de la ejecución</returns>
    [HttpGet("execute")]
    [EnableRateLimiting("DynamicPolicy")]
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
        int idToken = 0;

        try
        {
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

            // Obtener configuración de la API
            var config = await _configService.GetApiConfigurationAsync(idApi, requestContext?.Token);
            if (config == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "API_NOT_FOUND",
                    Message = $"API con ID {idApi} no encontrada o inactiva",
                    StatusCode = 404,
                    RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
                });
            }

            idToken = config.IdToken;

            // Extraer parámetros del query string
            var parameters = ExtractParametersFromQuery();

            // Obtener el conteo de parámetros sin "environment"
            var effectiveParameterCount = parameters.Count();
            if (parameters.ContainsKey("environment"))
            {
                effectiveParameterCount--;
            }

            // VALIDACIÓN de parámetros
            if (effectiveParameterCount > config.Parametros.Count())
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "INVALID_PARAMETERS",
                    Message = "El número de parámetros enviados no coincide con los esperados por la API.",
                    StatusCode = 400,
                    RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
                });
            }


            // Ejecutar la consulta SQL
            var result = await _sqlService.ExecuteSqlAsync(config, parameters, environment);

            success = true;
            stopwatch.Stop();

            var response = new ApiExecutionResponse
            {
                Success = true,
                Data = result,
                Message = "Ejecución exitosa",
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
            };

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_PARAMETERS",
                Message = ex.Message,
                StatusCode = 400,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
            });
        }
        catch (InvalidOperationException ex)
        {
            errorMessage = ex.Message;
            return BadRequest(new ErrorResponse
            {
                Error = "EXECUTION_ERROR",
                Message = ex.Message,
                StatusCode = 400,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
            });
        }
        catch (TimeoutException ex)
        {
            errorMessage = ex.Message;
            return StatusCode(408, new ErrorResponse
            {
                Error = "TIMEOUT",
                Message = "La consulta excedió el tiempo límite de ejecución",
                StatusCode = 408,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
            });
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger.LogError(ex, "Error ejecutando API {IdAPI}", idApi);

            if (ex.Message == "No Existe API" || ex.Message== "Token inválido o expirado")
                return StatusCode(500, new ErrorResponse
                {
                    Error = "INTERNAL_ERROR",
                    Message = ex.Message,
                    StatusCode = 500,
                    RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
                });

            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error interno del servidor",
                StatusCode = 500,
                RequestId = requestContext?.RequestId ?? Guid.NewGuid().ToString()
            });
        }
        finally
        {
            stopwatch.Stop();

            // Registrar auditoría
            _ = Task.Run(async () =>
            {
                try
                {
                    var auditLog = new AuditLog
                    {
                        IdAPI = idApi,
                        IdToken = idToken,
                        Ambiente = environment,
                        ParametrosEnviados = JsonSerializer.Serialize(ExtractParametersFromQuery()),
                        EsExitoso = success,
                        MensajeError = errorMessage,
                        TiempoEjecucionMs = (int)stopwatch.ElapsedMilliseconds,
                        DireccionIP = requestContext?.ClientIP
                    };

                    await _auditService.LogExecutionAsync(auditLog);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error registrando auditoría para API {IdAPI}", idApi);
                }
            });
        }
    }

    private Dictionary<string, object?> ExtractParametersFromQuery()
    {
        var parameters = new Dictionary<string, object?>();

        foreach (var query in Request.Query)
        {
            // Excluir parámetros del sistema
            if (IsSystemParameter(query.Key.ToLower()))
                continue;

            parameters[query.Key] = query.Value.FirstOrDefault();
        }

        return parameters;
    }

    private bool IsSystemParameter(string parameterName)
    {
        var systemParams = new[] { "idapi" };
        return systemParams.Contains(parameterName);
    }

    private bool IsValidEnvironment(string environment)
    {
        return environment.ToUpper() is "TEST" or "PRODUCTION";
    }
}