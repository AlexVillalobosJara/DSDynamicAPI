using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/info")]
public class InfoController : ControllerBase
{
    private readonly IConfigurationService _configService;
    private readonly IAuditService _auditService;
    private readonly ILogger<InfoController> _logger;

    public InfoController(
        IConfigurationService configService,
        IAuditService auditService,
        ILogger<InfoController> logger)
    {
        _configService = configService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene la lista de APIs disponibles y su documentación
    /// </summary>
    [HttpGet("apis")]
    [ProducesResponseType(typeof(List<ApiInfo>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetAvailableApis()
    {
        try
        {
            var apis = await _configService.GetAvailableApisAsync();
            return Ok(apis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo APIs disponibles");
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error obteniendo APIs disponibles",
                StatusCode = 500
            });
        }
    }

    /// <summary>
    /// Obtiene información específica de una API
    /// </summary>
    [HttpGet("apis/{idApi}")]
    [ProducesResponseType(typeof(ApiInfo), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetApiInfo(int idApi)
    {
        try
        {
            var apis = await _configService.GetAvailableApisAsync();
            var apiInfo = apis.FirstOrDefault(a => a.IdAPI == idApi);

            if (apiInfo == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = "API_NOT_FOUND",
                    Message = $"API con ID {idApi} no encontrada",
                    StatusCode = 404
                });
            }

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
    /// Obtiene estadísticas de uso de las APIs
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(List<UsageStatistics>), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> GetUsageStatistics(
        [FromQuery] int? idApi = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null)
    {
        try
        {
            var statistics = await _auditService.GetUsageStatisticsAsync(idApi, fechaDesde, fechaHasta);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas de uso");
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error obteniendo estadísticas de uso",
                StatusCode = 500
            });
        }
    }
}