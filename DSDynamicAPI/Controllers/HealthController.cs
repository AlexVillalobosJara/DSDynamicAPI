using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Health check de la API
    /// </summary>
    [HttpGet]
    [ProducesResponseType(200)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }

    /// <summary>
    /// Health check detallado con verificación de dependencias
    /// </summary>
    [HttpGet("detailed")]
    [ProducesResponseType(200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> GetDetailedHealth([FromServices] IConfigurationService configService)
    {
        var healthChecks = new Dictionary<string, object>();
        var overallStatus = "Healthy";

        try
        {
            // Verificar conexión a base de datos de configuración
            var apis = await configService.GetAvailableApisAsync();
            healthChecks["Database"] = new { Status = "Healthy", ApiCount = apis.Count };
        }
        catch (Exception ex)
        {
            healthChecks["Database"] = new { Status = "Unhealthy", Error = ex.Message };
            overallStatus = "Unhealthy";
        }

        // Verificar memoria y rendimiento
        var gcMemory = GC.GetTotalMemory(false);
        healthChecks["Memory"] = new
        {
            Status = gcMemory < 500_000_000 ? "Healthy" : "Warning", // 500MB threshold
            UsedBytes = gcMemory,
            UsedMB = gcMemory / 1024 / 1024
        };

        var response = new
        {
            Status = overallStatus,
            Timestamp = DateTime.UtcNow,
            Checks = healthChecks,
            Version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0"
        };

        return overallStatus == "Healthy" ? Ok(response) : StatusCode(503, response);
    }
}