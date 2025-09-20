// =====================================================
// CredencialService - IMPLEMENTACIÓN COMPLETA
// =====================================================

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using DSDynamicAPI.Services;
using DynamicAPIs.Services.Database;

namespace DynamicAPIs.Services.Implementation;

public class CredencialService : ICredencialService
{
    private readonly DatabaseService _dbService;
    private readonly ILogger<CredencialService> _logger;
    private readonly IAuthenticationService _authService;

    public CredencialService(
        DatabaseService dbService,
        ILogger<CredencialService> logger,
        IAuthenticationService authService)
    {
        _dbService = dbService;
        _logger = logger;
        _authService = authService;
    }

    // =====================================================
    // GESTIÓN BÁSICA DE CREDENCIALES
    // =====================================================

    public async Task<List<CredencialAPIDto>> GetCredencialsByAPIAsync(int idAPI)
    {
        const string sql = @"
            SELECT 
                c.IdCredencial, c.IdAPI, c.Nombre, c.ValorCredencial, c.ConfiguracionExtra,
                c.FechaCreacion, c.FechaExpiracion, c.UltimoUso, c.ContadorUsos, c.EsActivo, c.CreadoPor,
                ta.Codigo as TipoAutenticacion, ta.Nombre as NombreTipoAuth
            FROM CredencialesAPI c
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE c.IdAPI = @IdAPI
            ORDER BY c.FechaCreacion DESC";

        var credenciales = await _dbService.QueryAsync<CredencialAPIDto>(sql, new { IdAPI = idAPI });

        // Procesar propiedades calculadas
        foreach (var credencial in credenciales)
        {
            ProcessCredentialProperties(credencial);
        }

        _logger.LogInformation("Obtenidas {Count} credenciales para API {IdAPI}", credenciales.Count(), idAPI);
        return credenciales.ToList();
    }

    public async Task<CredencialAPIDto?> GetCredencialByIdAsync(int idCredencial)
    {
        const string sql = @"
            SELECT 
                c.IdCredencial, c.IdAPI, c.Nombre, c.ValorCredencial, c.ConfiguracionExtra,
                c.FechaCreacion, c.FechaExpiracion, c.UltimoUso, c.ContadorUsos, c.EsActivo, c.CreadoPor,
                ta.Codigo as TipoAutenticacion, ta.Nombre as NombreTipoAuth
            FROM CredencialesAPI c
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE c.IdCredencial = @IdCredencial";

        var credencial = await _dbService.QueryFirstOrDefaultAsync<CredencialAPIDto>(sql, new { IdCredencial = idCredencial });

        if (credencial != null)
        {
            ProcessCredentialProperties(credencial);
            _logger.LogInformation("Credencial {IdCredencial} obtenida exitosamente", idCredencial);
        }

        return credencial;
    }

    public async Task<CredencialAPIDto?> GetCredencialByValueAsync(string valorCredencial)
    {
        const string sql = @"
            SELECT 
                c.IdCredencial, c.IdAPI, c.Nombre, c.ValorCredencial, c.ConfiguracionExtra,
                c.FechaCreacion, c.FechaExpiracion, c.UltimoUso, c.ContadorUsos, c.EsActivo, c.CreadoPor,
                ta.Codigo as TipoAutenticacion, ta.Nombre as NombreTipoAuth
            FROM CredencialesAPI c
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE c.ValorCredencial = @ValorCredencial";

        var credencial = await _dbService.QueryFirstOrDefaultAsync<CredencialAPIDto>(sql, new { ValorCredencial = valorCredencial });

        if (credencial != null)
        {
            ProcessCredentialProperties(credencial);
        }

        return credencial;
    }

    public async Task<List<CredencialAPIDto>> GetAllCredencialesAsync()
    {
        const string sql = @"
            SELECT 
                c.IdCredencial, c.IdAPI, c.Nombre, c.ValorCredencial, c.ConfiguracionExtra,
                c.FechaCreacion, c.FechaExpiracion, c.UltimoUso, c.ContadorUsos, c.EsActivo, c.CreadoPor,
                ta.Codigo as TipoAutenticacion, ta.Nombre as NombreTipoAuth,
                a.NombreAPI
            FROM CredencialesAPI c
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            ORDER BY c.FechaCreacion DESC";

        var credenciales = await _dbService.QueryAsync<CredencialAPIDto>(sql);

        foreach (var credencial in credenciales)
        {
            ProcessCredentialProperties(credencial);
        }

        _logger.LogInformation("Obtenidas {Count} credenciales totales del sistema", credenciales.Count());
        return credenciales.ToList();
    }

    public async Task<List<CredencialAPIDto>> GetCredencialesByTypeAsync(TipoAutenticacion tipoAuth)
    {
        const string sql = @"
            SELECT 
                c.IdCredencial, c.IdAPI, c.Nombre, c.ValorCredencial, c.ConfiguracionExtra,
                c.FechaCreacion, c.FechaExpiracion, c.UltimoUso, c.ContadorUsos, c.EsActivo, c.CreadoPor,
                ta.Codigo as TipoAutenticacion, ta.Nombre as NombreTipoAuth,
                a.NombreAPI
            FROM CredencialesAPI c
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            WHERE ta.Codigo = @TipoAuth
            ORDER BY c.FechaCreacion DESC";

        var credenciales = await _dbService.QueryAsync<CredencialAPIDto>(sql, new { TipoAuth = tipoAuth.ToString() });

        foreach (var credencial in credenciales)
        {
            ProcessCredentialProperties(credencial);
        }

        _logger.LogInformation("Obtenidas {Count} credenciales de tipo {TipoAuth}", credenciales.Count(), tipoAuth);
        return credenciales.ToList();
    }

    // =====================================================
    // CREACIÓN Y GESTIÓN DE CREDENCIALES
    // =====================================================

    public async Task<int> CreateCredencialAsync(CredencialAPIDto credencial)
    {
        const string sql = @"
            INSERT INTO CredencialesAPI (
                IdAPI, IdTipoAuth, Nombre, ValorCredencial, ConfiguracionExtra, 
                FechaExpiracion, EsActivo, CreadoPor
            )
            SELECT 
                @IdAPI, ta.IdTipoAuth, @Nombre, @ValorCredencial, @ConfiguracionExtra,
                @FechaExpiracion, @EsActivo, @CreadoPor
            FROM TiposAutenticacion ta
            WHERE ta.Codigo = @TipoAutenticacion;
            
            SELECT SCOPE_IDENTITY();";

        var idCredencial = await _dbService.QueryFirstOrDefaultAsync<int>(sql, new
        {
            credencial.IdAPI,
            TipoAutenticacion = credencial.TipoAutenticacion,
            credencial.Nombre,
            credencial.ValorCredencial,
            credencial.ConfiguracionExtra,
            credencial.FechaExpiracion,
            credencial.EsActivo,
            credencial.CreadoPor
        });

        _logger.LogInformation("Credencial creada exitosamente: ID={IdCredencial}, API={IdAPI}, Tipo={TipoAuth}",
            idCredencial, credencial.IdAPI, credencial.TipoAutenticacion);

        return idCredencial;
    }

    public async Task<bool> UpdateCredencialAsync(CredencialAPIDto credencial)
    {
        const string sql = @"
            UPDATE CredencialesAPI 
            SET Nombre = @Nombre,
                ConfiguracionExtra = @ConfiguracionExtra,
                FechaExpiracion = @FechaExpiracion,
                EsActivo = @EsActivo
            WHERE IdCredencial = @IdCredencial";

        var rowsAffected = await _dbService.ExecuteAsync(sql, new
        {
            credencial.IdCredencial,
            credencial.Nombre,
            credencial.ConfiguracionExtra,
            credencial.FechaExpiracion,
            credencial.EsActivo
        });

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Credencial actualizada: ID={IdCredencial}", credencial.IdCredencial);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteCredencialAsync(int idCredencial)
    {
        const string sql = "DELETE FROM CredencialesAPI WHERE IdCredencial = @IdCredencial";
        var rowsAffected = await _dbService.ExecuteAsync(sql, new { IdCredencial = idCredencial });

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Credencial eliminada: ID={IdCredencial}", idCredencial);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> ToggleCredencialStatusAsync(int idCredencial)
    {
        const string sql = @"
            UPDATE CredencialesAPI 
            SET EsActivo = CASE WHEN EsActivo = 1 THEN 0 ELSE 1 END
            WHERE IdCredencial = @IdCredencial";

        var rowsAffected = await _dbService.ExecuteAsync(sql, new { IdCredencial = idCredencial });

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Estado de credencial cambiado: ID={IdCredencial}", idCredencial);
        }

        return rowsAffected > 0;
    }

    // =====================================================
    // GENERACIÓN AUTOMÁTICA DE CREDENCIALES
    // =====================================================

    public async Task<string> GenerateTokenAsync(int idAPI, int diasExpiracion = 365, string? creadoPor = null)
    {
        var tokenValue = GenerateSecureToken();
        var nombre = $"Token-{DateTime.Now:yyyyMMddHHmmss}";

        var credencial = new CredencialAPIDto
        {
            IdAPI = idAPI,
            TipoAutenticacion = "TOKEN",
            Nombre = nombre,
            ValorCredencial = tokenValue,
            FechaExpiracion = DateTime.Now.AddDays(diasExpiracion),
            EsActivo = true,
            CreadoPor = creadoPor ?? "SYSTEM"
        };

        await CreateCredencialAsync(credencial);

        _logger.LogInformation("Token generado para API {IdAPI}, expira en {Dias} días", idAPI, diasExpiracion);

        return tokenValue;
    }

    public async Task<string> GenerateApiKeyAsync(int idAPI, int? diasExpiracion = null, string? creadoPor = null)
    {
        var apiKeyValue = GenerateSecureApiKey();
        var nombre = $"ApiKey-{DateTime.Now:yyyyMMddHHmmss}";

        var credencial = new CredencialAPIDto
        {
            IdAPI = idAPI,
            TipoAutenticacion = "APIKEY",
            Nombre = nombre,
            ValorCredencial = apiKeyValue,
            FechaExpiracion = diasExpiracion.HasValue ? DateTime.Now.AddDays(diasExpiracion.Value) : null,
            EsActivo = true,
            CreadoPor = creadoPor ?? "SYSTEM"
        };

        await CreateCredencialAsync(credencial);

        _logger.LogInformation("API Key generada para API {IdAPI}", idAPI);

        return apiKeyValue;
    }

    public async Task<CredencialAPIDto> CreateJWTCredentialAsync(int idAPI, string valorJWT, object? configuracion = null, int? diasExpiracion = null, string? creadoPor = null)
    {
        var nombre = $"JWT-{DateTime.Now:yyyyMMddHHmmss}";
        var configJson = configuracion != null ? JsonSerializer.Serialize(configuracion) : null;

        var credencial = new CredencialAPIDto
        {
            IdAPI = idAPI,
            TipoAutenticacion = "JWT",
            Nombre = nombre,
            ValorCredencial = valorJWT,
            ConfiguracionExtra = configJson,
            FechaExpiracion = diasExpiracion.HasValue ? DateTime.Now.AddDays(diasExpiracion.Value) : null,
            EsActivo = true,
            CreadoPor = creadoPor ?? "SYSTEM"
        };

        credencial.IdCredencial = await CreateCredencialAsync(credencial);

        _logger.LogInformation("Credencial JWT creada para API {IdAPI}", idAPI);

        return credencial;
    }

    // =====================================================
    // VALIDACIÓN Y AUTENTICACIÓN
    // =====================================================

    public async Task<AuthValidationResult> ValidateCredentialAsync(CredentialValidationRequest request)
    {
        try
        {
            // Delegar al servicio de autenticación principal
            return await _authService.AuthenticateAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando credencial para API {IdAPI}", request.IdAPI);
            return new AuthValidationResult
            {
                IsValid = false,
                ErrorMessage = "Error interno validando credencial",
                IdAPI = request.IdAPI
            };
        }
    }

    public async Task<bool> ValidateCredentialSimpleAsync(string valorCredencial, int? idAPI = null)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM CredencialesAPI c
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            WHERE c.ValorCredencial = @ValorCredencial
            AND c.EsActivo = 1
            AND a.EsActivo = 1
            AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE())
            AND (@IdAPI IS NULL OR c.IdAPI = @IdAPI)";

        var count = await _dbService.QueryFirstOrDefaultAsync<int>(sql, new { ValorCredencial = valorCredencial, IdAPI = idAPI });

        return count > 0;
    }

    public async Task<bool> CheckRateLimitAsync(int idCredencial)
    {
        // Delegar al servicio de autenticación
        return await _authService.CheckRateLimitAsync(idCredencial, 0); // IdAPI se obtiene internamente
    }

    // =====================================================
    // GESTIÓN DE EXPIRACIÓN
    // =====================================================

    public async Task<List<CredencialAPIDto>> GetExpiredCredentialsAsync()
    {
        const string sql = @"
            SELECT 
                c.IdCredencial, c.IdAPI, c.Nombre, c.ValorCredencial, c.ConfiguracionExtra,
                c.FechaCreacion, c.FechaExpiracion, c.UltimoUso, c.ContadorUsos, c.EsActivo, c.CreadoPor,
                ta.Codigo as TipoAutenticacion, ta.Nombre as NombreTipoAuth,
                a.NombreAPI
            FROM CredencialesAPI c
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            WHERE c.FechaExpiracion IS NOT NULL 
            AND c.FechaExpiracion <= GETDATE()
            AND c.EsActivo = 1
            ORDER BY c.FechaExpiracion";

        var credenciales = await _dbService.QueryAsync<CredencialAPIDto>(sql);

        foreach (var credencial in credenciales)
        {
            ProcessCredentialProperties(credencial);
        }

        _logger.LogInformation("Encontradas {Count} credenciales expiradas", credenciales.Count());
        return credenciales.ToList();
    }

    public async Task<List<CredencialAPIDto>> GetCredentialsExpiringSoonAsync(int days = 7)
    {
        const string sql = @"
            SELECT 
                c.IdCredencial, c.IdAPI, c.Nombre, c.ValorCredencial, c.ConfiguracionExtra,
                c.FechaCreacion, c.FechaExpiracion, c.UltimoUso, c.ContadorUsos, c.EsActivo, c.CreadoPor,
                ta.Codigo as TipoAutenticacion, ta.Nombre as NombreTipoAuth,
                a.NombreAPI
            FROM CredencialesAPI c
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            WHERE c.FechaExpiracion IS NOT NULL 
            AND c.FechaExpiracion > GETDATE()
            AND c.FechaExpiracion <= DATEADD(DAY, @Days, GETDATE())
            AND c.EsActivo = 1
            ORDER BY c.FechaExpiracion";

        var credenciales = await _dbService.QueryAsync<CredencialAPIDto>(sql, new { Days = days });

        foreach (var credencial in credenciales)
        {
            ProcessCredentialProperties(credencial);
        }

        _logger.LogInformation("Encontradas {Count} credenciales que expiran en {Days} días", credenciales.Count(), days);
        return credenciales.ToList();
    }

    public async Task<int> CleanupExpiredCredentialsAsync()
    {
        const string sql = @"
            UPDATE CredencialesAPI 
            SET EsActivo = 0
            WHERE FechaExpiracion IS NOT NULL 
            AND FechaExpiracion <= GETDATE()
            AND EsActivo = 1";

        var rowsAffected = await _dbService.ExecuteAsync(sql);

        _logger.LogInformation("Limpieza de credenciales expiradas: {Count} credenciales desactivadas", rowsAffected);
        return rowsAffected;
    }

    public async Task<bool> RevokeCredentialAsync(int idCredencial)
    {
        const string sql = @"
            UPDATE CredencialesAPI 
            SET EsActivo = 0, FechaExpiracion = GETDATE()
            WHERE IdCredencial = @IdCredencial";

        var rowsAffected = await _dbService.ExecuteAsync(sql, new { IdCredencial = idCredencial });

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Credencial revocada: ID={IdCredencial}", idCredencial);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> ExtendCredentialExpirationAsync(int idCredencial, int additionalDays)
    {
        const string sql = @"
            UPDATE CredencialesAPI 
            SET FechaExpiracion = CASE 
                WHEN FechaExpiracion IS NULL THEN DATEADD(DAY, @AdditionalDays, GETDATE())
                ELSE DATEADD(DAY, @AdditionalDays, FechaExpiracion)
            END
            WHERE IdCredencial = @IdCredencial";

        var rowsAffected = await _dbService.ExecuteAsync(sql, new { IdCredencial = idCredencial, AdditionalDays = additionalDays });

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Expiración extendida para credencial {IdCredencial} por {Days} días", idCredencial, additionalDays);
        }

        return rowsAffected > 0;
    }

    // =====================================================
    // ESTADÍSTICAS Y MONITOREO
    // =====================================================

    public async Task<int> GetActiveCredentialsCountAsync(int? idAPI = null)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM CredencialesAPI c
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            WHERE c.EsActivo = 1
            AND a.EsActivo = 1
            AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE())
            AND (@IdAPI IS NULL OR c.IdAPI = @IdAPI)";

        var count = await _dbService.QueryFirstOrDefaultAsync<int>(sql, new { IdAPI = idAPI });

        return count;
    }

    public async Task<List<CredencialAPIDto>> GetCredentialsExceedingLimitAsync()
    {
        const string sql = @"
            SELECT 
                c.IdCredencial, c.IdAPI, c.Nombre, c.ValorCredencial, c.ConfiguracionExtra,
                c.FechaCreacion, c.FechaExpiracion, c.UltimoUso, c.ContadorUsos, c.EsActivo, c.CreadoPor,
                ta.Codigo as TipoAutenticacion, ta.Nombre as NombreTipoAuth,
                a.NombreAPI, a.RateLimitPorMinuto,
                COUNT(al.IdCredencial) as UsosRecientes
            FROM CredencialesAPI c
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            LEFT JOIN AuditLogs al ON c.IdCredencial = al.IdCredencial 
                AND al.FechaEjecucion >= DATEADD(MINUTE, -1, GETDATE())
            WHERE c.EsActivo = 1
            AND a.EsActivo = 1
            AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE())
            GROUP BY 
                c.IdCredencial, c.IdAPI, c.Nombre, c.ValorCredencial, c.ConfiguracionExtra,
                c.FechaCreacion, c.FechaExpiracion, c.UltimoUso, c.ContadorUsos, c.EsActivo, c.CreadoPor,
                ta.Codigo, ta.Nombre, a.NombreAPI, a.RateLimitPorMinuto
            HAVING COUNT(al.IdCredencial) >= a.RateLimitPorMinuto";

        var credenciales = await _dbService.QueryAsync<CredencialAPIDto>(sql);

        foreach (var credencial in credenciales)
        {
            ProcessCredentialProperties(credencial);
        }

        _logger.LogInformation("Encontradas {Count} credenciales excediendo rate limit", credenciales.Count());
        return credenciales.ToList();
    }

    public async Task<Dictionary<TipoAutenticacion, int>> GetCredentialsByTypeStatsAsync()
    {
        const string sql = @"
            SELECT ta.Codigo, COUNT(c.IdCredencial) as Count
            FROM TiposAutenticacion ta
            LEFT JOIN CredencialesAPI c ON ta.IdTipoAuth = c.IdTipoAuth 
                AND c.EsActivo = 1 
                AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE())
            WHERE ta.EsActivo = 1
            GROUP BY ta.Codigo";

        var results = await _dbService.QueryAsync<dynamic>(sql);

        var stats = new Dictionary<TipoAutenticacion, int>();

        foreach (var result in results)
        {
            if (Enum.TryParse<TipoAutenticacion>(result.Codigo, out TipoAutenticacion tipoAuth))
            {
                stats[tipoAuth] = result.Count ?? 0;
            }
        }

        return stats;
    }

    // =====================================================
    // MÉTODOS AUXILIARES
    // =====================================================

    private void ProcessCredentialProperties(CredencialAPIDto credencial)
    {
        // Procesar propiedades calculadas
        credencial.EstaExpirada = credencial.FechaExpiracion.HasValue && credencial.FechaExpiracion.Value <= DateTime.Now;
        credencial.DaysUntilExpiration = credencial.FechaExpiracion?.Subtract(DateTime.Now).Days;

        // Crear valor enmascarado para mostrar
        if (!string.IsNullOrEmpty(credencial.ValorCredencial))
        {
            if (credencial.ValorCredencial.Length > 8)
            {
                credencial.ValorEnmascarado = credencial.ValorCredencial[..4] + "***" + credencial.ValorCredencial[^4..];
            }
            else
            {
                credencial.ValorEnmascarado = credencial.ValorCredencial[..2] + "***";
            }
        }

        // Textos para UI
        credencial.EstadoTexto = credencial.EsActivo
            ? (credencial.EstaExpirada ? "Expirada" : "Activa")
            : "Inactiva";

        credencial.FechaExpiracionTexto = credencial.FechaExpiracion?.ToString("dd/MM/yyyy") ?? "Sin expiración";
        credencial.UltimoUsoTexto = credencial.UltimoUso?.ToString("dd/MM/yyyy HH:mm") ?? "Nunca";
    }

    private string GenerateSecureToken()
    {
        const string prefix = "tk_";
        const int length = 32;

        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);

        var token = prefix + Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..length];

        return token;
    }

    private string GenerateSecureApiKey()
    {
        const string prefix = "ak_";
        const int length = 40;

        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);

        var apiKey = prefix + Convert.ToHexString(bytes)[..length];

        return apiKey.ToLower();
    }

    private async Task<bool> ValidateCredentialFormatAsync(string valorCredencial, TipoAutenticacion tipoAuth)
    {
        return tipoAuth switch
        {
            TipoAutenticacion.TOKEN => valorCredencial.StartsWith("tk_") && valorCredencial.Length >= 32,
            TipoAutenticacion.APIKEY => valorCredencial.StartsWith("ak_") && valorCredencial.Length >= 32,
            TipoAutenticacion.JWT => IsValidJWTFormat(valorCredencial),
            TipoAutenticacion.BASIC => IsValidBasicAuthFormat(valorCredencial),
            _ => true // Para otros tipos, asumimos válido
        };
    }

    private bool IsValidJWTFormat(string jwt)
    {
        var parts = jwt.Split('.');
        return parts.Length == 3 && parts.All(part => !string.IsNullOrEmpty(part));
    }

    private bool IsValidBasicAuthFormat(string basic)
    {
        try
        {
            if (basic.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                basic = basic["Basic ".Length..];
            }

            var bytes = Convert.FromBase64String(basic);
            var decoded = Encoding.UTF8.GetString(bytes);
            return decoded.Contains(':');
        }
        catch
        {
            return false;
        }
    }
}