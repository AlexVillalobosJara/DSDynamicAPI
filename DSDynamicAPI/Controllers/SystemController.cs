
// =====================================================
// SystemController - CONTROLLER PARA INFORMACIÓN DEL SISTEMA
// =====================================================
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

[ApiController]
[Route("api/system")]
[Produces("application/json")]
public class SystemController : ControllerBase
{
    private readonly IConfigurationService _configService;
    private readonly IAuditService _auditService;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        IConfigurationService configService,
        IAuditService auditService,
        IAuthenticationService authService,
        ILogger<SystemController> logger)
    {
        _configService = configService;
        _auditService = auditService;
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene estadísticas generales del sistema
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetSystemStats()
    {
        try
        {
            var apis = await _configService.GetAvailableApisAsync();
            var authHealth = await _authService.GetSystemAuthHealthAsync();
            var totalExecutions = await _auditService.GetTotalExecutionsAsync(DateTime.Now.AddDays(-30));
            var avgExecutionTime = await _auditService.GetAverageExecutionTimeAsync(null, DateTime.Now.AddDays(-30));
            var successRate = await _auditService.GetSuccessRateAsync(null, DateTime.Now.AddDays(-30));

            var stats = new
            {
                System = new
                {
                    Version = "2.0.0",
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime(),
                    Timestamp = DateTime.UtcNow
                },
                APIs = new
                {
                    Total = apis.Count,
                    authHealth.APIsWithAuth,
                    PublicAPIs = authHealth.TotalAPIs - authHealth.APIsWithAuth
                },
                Authentication = new
                {
                    authHealth.IsHealthy,
                    authHealth.TotalCredentials,
                    authHealth.ActiveCredentials,
                    authHealth.ExpiredCredentials,
                    authHealth.AuthTypeDistribution
                },
                Usage = new
                {
                    TotalExecutions = totalExecutions,
                    AvgExecutionTimeMs = Math.Round(avgExecutionTime, 2),
                    SuccessRate = Math.Round(successRate, 2),
                    Period = "Last 30 days"
                },
                Health = new
                {
                    Status = authHealth.IsHealthy ? "Healthy" : "Warning",
                    Warnings = authHealth.Warnings,
                    Errors = authHealth.Errors
                }
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas del sistema");
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error obteniendo estadísticas del sistema",
                StatusCode = 500
            });
        }
    }

    /// <summary>
    /// Obtiene logs de errores recientes
    /// </summary>
    [HttpGet("errors")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetRecentErrors([FromQuery] int count = 10)
    {
        try
        {
            var errors = await _auditService.GetRecentErrorsAsync(count);

            return Ok(new
            {
                Success = true,
                Count = errors.Count,
                Errors = errors.Select(e => new
                {
                    e.IdAPI,
                    e.NombreAPI,
                    e.MensajeError,
                    e.FechaEjecucion,
                    e.DireccionIP,
                    e.TipoAutenticacion,
                    e.NombreCredencial
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo errores recientes");
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error obteniendo errores del sistema",
                StatusCode = 500
            });
        }
    }
}