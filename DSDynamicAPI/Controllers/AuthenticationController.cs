
// =====================================================
// AuthenticationController - NUEVO CONTROLLER PARA GESTIÓN DE AUTH
// =====================================================
using DynamicAPIs.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ITipoAutenticacionService _tipoAuthService;
    private readonly ICredencialService _credencialService;
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(
        IAuthenticationService authService,
        ITipoAutenticacionService tipoAuthService,
        ICredencialService credencialService,
        ILogger<AuthenticationController> logger)
    {
        _authService = authService;
        _tipoAuthService = tipoAuthService;
        _credencialService = credencialService;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene los tipos de autenticación disponibles
    /// </summary>
    [HttpGet("types")]
    [ProducesResponseType(typeof(List<TipoAutenticacionDto>), 200)]
    public async Task<IActionResult> GetAuthTypes()
    {
        try
        {
            var tipos = await _tipoAuthService.GetActiveTiposAsync();
            return Ok(new
            {
                Success = true,
                Count = tipos.Count,
                AuthTypes = tipos.Select(t => new
                {
                    t.IdTipoAuth,
                    t.Codigo,
                    t.Nombre,
                    t.Descripcion,
                    t.RequiereConfiguracion,
                    HeaderExample = GetAuthHeaderExample(t.Codigo)
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo tipos de autenticación");
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error obteniendo tipos de autenticación",
                StatusCode = 500
            });
        }
    }

    /// <summary>
    /// Valida una credencial específica
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    public async Task<IActionResult> ValidateCredential([FromBody] CredentialValidationRequest request)
    {
        try
        {
            if (request.IdAPI <= 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "INVALID_REQUEST",
                    Message = "IdAPI es requerido",
                    StatusCode = 400
                });
            }

            var result = await _authService.AuthenticateAsync(request);

            return Ok(new
            {
                Success = result.IsValid,
                result.TipoAuth,
                result.RateLimitExceeded,
                result.RemainingRequests,
                ErrorMessage = result.ErrorMessage,
                Metadata = result.AuthMetadata
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando credencial para API {IdAPI}", request.IdAPI);
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error validando credencial",
                StatusCode = 500
            });
        }
    }

    /// <summary>
    /// Obtiene estadísticas del sistema de autenticación
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(SystemAuthHealthDto), 200)]
    public async Task<IActionResult> GetAuthHealth()
    {
        try
        {
            var health = await _authService.GetSystemAuthHealthAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo salud del sistema de autenticación");
            return StatusCode(500, new ErrorResponse
            {
                Error = "INTERNAL_ERROR",
                Message = "Error obteniendo estado del sistema",
                StatusCode = 500
            });
        }
    }

    private string GetAuthHeaderExample(string authType)
    {
        return authType switch
        {
            "TOKEN" => "X-API-Token: your_token_here",
            "APIKEY" => "X-API-Key: your_api_key_here",
            "JWT" => "Authorization: Bearer your_jwt_token_here",
            "OAUTH2" => "Authorization: Bearer your_oauth2_token_here",
            "NTLM" => "Authorization: NTLM your_ntlm_token_here",
            "BASIC" => "Authorization: Basic base64(username:password)",
            "NONE" => "No authentication required",
            _ => "See API documentation"
        };
    }
}
