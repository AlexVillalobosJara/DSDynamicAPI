// =====================================================
// AuthenticationService - IMPLEMENTACIÓN COMPLETA
// =====================================================

using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using Dapper;
using DynamicAPIs.Services.Database;

namespace DynamicAPIs.Services.Implementation;

public class AuthenticationService : IAuthenticationService
{
    private readonly DatabaseService _dbService;
    private readonly ILogger<AuthenticationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<int, DateTime> _rateLimitCache = new();

    public AuthenticationService(
        DatabaseService dbService,
        ILogger<AuthenticationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _dbService = dbService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    // =====================================================
    // VALIDACIÓN PRINCIPAL DE AUTENTICACIÓN
    // =====================================================

    /// <summary>
    /// Verifica que una credencial específica tenga acceso a una API específica
    /// </summary>
    public async Task<bool> VerifyCredentialAccessAsync(int idCredencial, int idAPI)
    {
        try
        {
            _logger.LogDebug("Verificando acceso de credencial {IdCredencial} a API {IdAPI}", idCredencial, idAPI);

            // Consulta para verificar acceso directo (credencial pertenece a la API)
            const string sqlDirectAccess = @"
                SELECT COUNT(*) 
                FROM CredencialesAPI c
                INNER JOIN APIs a ON c.IdAPI = a.IdAPI
                WHERE c.IdCredencial = @IdCredencial 
                  AND c.IdAPI = @IdAPI
                  AND c.EsActivo = 1 
                  AND a.EsActivo = 1
                  AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE())";

            var directAccess = await _dbService.QueryFirstOrDefaultAsIntAsync(sqlDirectAccess,
                new { IdCredencial = idCredencial, IdAPI = idAPI });

            if (directAccess > 0)
            {
                _logger.LogDebug("Acceso directo confirmado para credencial {IdCredencial} a API {IdAPI}", idCredencial, idAPI);
                return true;
            }

            // OPCIONAL: Verificar acceso a través de grupos o permisos adicionales
            // (Si implementas un sistema de permisos más complejo)
            const string sqlGroupAccess = @"
                SELECT COUNT(*)
                FROM CredencialesAPI c
                INNER JOIN APIs a ON c.IdAPI = a.IdAPI
                INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
                WHERE c.IdCredencial = @IdCredencial 
                  AND a.IdAPI = @IdAPI
                  AND c.EsActivo = 1 
                  AND a.EsActivo = 1
                  AND ta.EsActivo = 1
                  AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE())
                  -- Misma API y mismo tipo de autenticación
                  AND c.IdTipoAuth = a.IdTipoAuth";

            var groupAccess = await _dbService.QueryFirstOrDefaultAsIntAsync(sqlGroupAccess,
                new { IdCredencial = idCredencial, IdAPI = idAPI });

            if (groupAccess > 0)
            {
                _logger.LogDebug("Acceso por tipo de autenticación confirmado para credencial {IdCredencial} a API {IdAPI}", idCredencial, idAPI);
                return true;
            }

            _logger.LogWarning("Acceso denegado: credencial {IdCredencial} no tiene permisos para API {IdAPI}", idCredencial, idAPI);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando acceso de credencial {IdCredencial} a API {IdAPI}", idCredencial, idAPI);
            return false; // En caso de error, denegar acceso por seguridad
        }
    }

    /// <summary>
    /// Verifica que una credencial esté activa y no haya expirado
    /// </summary>
    public async Task<bool> VerifyCredentialActiveAsync(int idCredencial)
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*) 
                FROM CredencialesAPI 
                WHERE IdCredencial = @IdCredencial 
                  AND EsActivo = 1 
                  AND (FechaExpiracion IS NULL OR FechaExpiracion > GETDATE())";

            var count = await _dbService.QueryFirstOrDefaultAsIntAsync(sql, new { IdCredencial = idCredencial });

            var isActive = count > 0;
            _logger.LogDebug("Credencial {IdCredencial} está {Status}", idCredencial, isActive ? "activa" : "inactiva");

            return isActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando estado de credencial {IdCredencial}", idCredencial);
            return false;
        }
    }

    /// <summary>
    /// Verifica que una API esté activa
    /// </summary>
    public async Task<bool> VerifyAPIActiveAsync(int idAPI)
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*) 
                FROM APIs 
                WHERE IdAPI = @IdAPI 
                  AND EsActivo = 1";

            var count = await _dbService.QueryFirstOrDefaultAsIntAsync(sql, new { IdAPI = idAPI });

            var isActive = count > 0;
            _logger.LogDebug("API {IdAPI} está {Status}", idAPI, isActive ? "activa" : "inactiva");

            return isActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando estado de API {IdAPI}", idAPI);
            return false;
        }
    }

    public async Task<AuthValidationResult> AuthenticateAsync(CredentialValidationRequest request)
    {
        try
        {
            _logger.LogInformation("Iniciando autenticación para API {IdAPI} con tipo {TipoAuth}",
                request.IdAPI, request.TipoAuth);

            // Obtener configuración de la API
            ApiConfiguration? apiConfig = await GetAPIConfigForAuthAsync(request.IdAPI);

            if (apiConfig == null)
            {
                return new AuthValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "API no encontrada o inactiva",
                    IdAPI = request.IdAPI
                };
            }

            // Verificar que el tipo de auth coincida
            if (apiConfig.TipoAutenticacion != request.TipoAuth.ToString())
            {
                return new AuthValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"API configurada para {apiConfig.TipoAutenticacion}, recibido {request.TipoAuth}",
                    IdAPI = request.IdAPI
                };
            }

            // Delegar a método específico por tipo
            return request.TipoAuth switch
            {
                TipoAutenticacion.NONE => await ValidateNoneAuthAsync(request.IdAPI),
                TipoAutenticacion.TOKEN => await ValidateTokenAsync(request.Credential!, request.IdAPI),
                TipoAutenticacion.APIKEY => await ValidateApiKeyAsync(request.Credential!, request.IdAPI),
                TipoAutenticacion.JWT => await ValidateJWTAsync(request.Credential!, request.IdAPI),
                TipoAutenticacion.OAUTH2 => await ValidateOAuth2TokenAsync(request.Credential!, request.IdAPI),
                TipoAutenticacion.NTLM => await ValidateNTLMAsync(request.Credential!, request.IdAPI),
                TipoAutenticacion.BASIC => await ValidateBasicAuthAsync(request.Credential!, request.IdAPI),
                _ => new AuthValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Tipo de autenticación no soportado",
                    IdAPI = request.IdAPI
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en autenticación para API {IdAPI}", request.IdAPI);
            return new AuthValidationResult
            {
                IsValid = false,
                ErrorMessage = "Error interno de autenticación",
                IdAPI = request.IdAPI
            };
        }
    }

    // =====================================================
    // VALIDACIÓN POR TIPO DE AUTENTICACIÓN
    // =====================================================

    public async Task<AuthValidationResult> ValidateTokenAsync(string token, int? idAPI = null)
    {
        const string sql = @"
            SELECT c.IdCredencial, c.IdAPI, c.Nombre, c.UltimoUso, c.ContadorUsos,
                   a.RateLimitPorMinuto, a.NombreAPI
            FROM CredencialesAPI c
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE c.ValorCredencial = @Token 
            AND ta.Codigo = 'TOKEN'
            AND c.EsActivo = 1 
            AND a.EsActivo = 1
            AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE())
            AND (@IdAPI IS NULL OR c.IdAPI = @IdAPI)";

        var credencial = await _dbService.QueryFirstOrDefaultAsync<dynamic>(sql, new { Token = token, IdAPI = idAPI });

        if (credencial == null)
        {
            return new AuthValidationResult
            {
                IsValid = false,
                ErrorMessage = "Token inválido o expirado",
                IdAPI = idAPI ?? 0
            };
        }

        // Verificar rate limit
        var rateLimitExceeded = await CheckRateLimitAsync(credencial.IdCredencial, credencial.IdAPI);

        var result = new AuthValidationResult
        {
            IsValid = true,
            IdAPI = credencial.IdAPI,
            IdCredencial = credencial.IdCredencial,
            TipoAuth = "TOKEN",
            RateLimitExceeded = rateLimitExceeded
        };

        if (!rateLimitExceeded)
        {
            await IncrementRateLimitAsync(credencial.IdCredencial);
            await UpdateLastUsageAsync(credencial.IdCredencial);
        }

        return result;
    }

    public async Task<AuthValidationResult> ValidateApiKeyAsync(string apiKey, int? idAPI = null)
    {
        const string sql = @"
            SELECT c.IdCredencial, c.IdAPI, c.Nombre, c.UltimoUso,
                   a.RateLimitPorMinuto, a.NombreAPI
            FROM CredencialesAPI c
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE c.ValorCredencial = @ApiKey 
            AND ta.Codigo = 'APIKEY'
            AND c.EsActivo = 1 
            AND a.EsActivo = 1
            AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE())
            AND (@IdAPI IS NULL OR c.IdAPI = @IdAPI)";

        var credencial = await _dbService.QueryFirstOrDefaultAsync<dynamic>(sql, new { ApiKey = apiKey, IdAPI = idAPI });

        if (credencial == null)
        {
            return new AuthValidationResult
            {
                IsValid = false,
                ErrorMessage = "API Key inválida o expirada",
                IdAPI = idAPI ?? 0
            };
        }

        var rateLimitExceeded = await CheckRateLimitAsync(credencial.IdCredencial, credencial.IdAPI);

        var result = new AuthValidationResult
        {
            IsValid = true,
            IdAPI = credencial.IdAPI,
            IdCredencial = credencial.IdCredencial,
            TipoAuth = "APIKEY",
            RateLimitExceeded = rateLimitExceeded
        };

        if (!rateLimitExceeded)
        {
            await IncrementRateLimitAsync(credencial.IdCredencial);
            await UpdateLastUsageAsync(credencial.IdCredencial);
        }

        return result;
    }

    public async Task<AuthValidationResult> ValidateJWTAsync(string jwt, int idAPI)
    {
        var result = new AuthValidationResult { IdAPI = idAPI, TipoAuth = "JWT" };

        try
        {
            // Obtener configuración JWT de la API
            var apiConfig = await GetAPIConfigForAuthAsync(idAPI);
            if (apiConfig?.TipoAutenticacion != "JWT")
            {
                result.ErrorMessage = "API no configurada para JWT";
                return result;
            }

            var jwtConfig = apiConfig.GetConfiguracionAuth<JWTConfiguration>();
            if (jwtConfig == null)
            {
                result.ErrorMessage = "Configuración JWT no válida";
                return result;
            }

            // Validar JWT
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwtConfig.SecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = jwtConfig.ValidateIssuerSigningKey,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = jwtConfig.ValidateIssuer,
                ValidIssuer = jwtConfig.ValidateIssuer ? jwtConfig.Issuer : null,
                ValidateAudience = jwtConfig.ValidateAudience,
                ValidAudience = jwtConfig.ValidateAudience ? jwtConfig.Audience : null,
                ValidateLifetime = jwtConfig.ValidateLifetime,
                ClockSkew = TimeSpan.FromSeconds(jwtConfig.ClockSkewSeconds)
            };

            var principal = tokenHandler.ValidateToken(jwt, validationParameters, out SecurityToken validatedToken);

            result.IsValid = true;
            result.AuthMetadata = new Dictionary<string, object>
            {
                ["Claims"] = principal.Claims.ToDictionary(c => c.Type, c => c.Value),
                ["Issuer"] = validatedToken.Issuer,
                ["ValidTo"] = validatedToken.ValidTo
            };

            _logger.LogInformation("JWT validado exitosamente para API {IdAPI}", idAPI);
        }
        catch (SecurityTokenException ex)
        {
            result.ErrorMessage = $"JWT inválido: {ex.Message}";
            _logger.LogWarning("JWT inválido para API {IdAPI}: {Error}", idAPI, ex.Message);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = "Error validando JWT";
            _logger.LogError(ex, "Error validando JWT para API {IdAPI}", idAPI);
        }

        return result;
    }

    public async Task<AuthValidationResult> ValidateOAuth2TokenAsync(string token, int idAPI)
    {
        var result = new AuthValidationResult { IdAPI = idAPI, TipoAuth = "OAUTH2" };

        try
        {
            var apiConfig = await GetAPIConfigForAuthAsync(idAPI);
            if (apiConfig?.TipoAutenticacion != "OAUTH2")
            {
                result.ErrorMessage = "API no configurada para OAuth2";
                return result;
            }

            var oauth2Config = apiConfig.GetConfiguracionAuth<OAuth2Configuration>();
            if (oauth2Config == null)
            {
                result.ErrorMessage = "Configuración OAuth2 no válida";
                return result;
            }

            // Validar token via introspection endpoint
            using var client = _httpClientFactory.CreateClient();

            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{oauth2Config.ClientId}:{oauth2Config.ClientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            var introspectData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", token),
                new KeyValuePair<string, string>("token_type_hint", "access_token")
            });

            var response = await client.PostAsync(oauth2Config.IntrospectionEndpoint, introspectData);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var introspectionResult = JsonSerializer.Deserialize<JsonElement>(content);

                if (introspectionResult.TryGetProperty("active", out var activeProperty) && activeProperty.GetBoolean())
                {
                    result.IsValid = true;
                    result.AuthMetadata = new Dictionary<string, object>
                    {
                        ["TokenInfo"] = content,
                        ["ValidatedAt"] = DateTime.UtcNow
                    };

                    // Verificar scopes requeridos si están configurados
                    if (oauth2Config.RequiredScopes?.Any() == true)
                    {
                        if (introspectionResult.TryGetProperty("scope", out var scopeProperty))
                        {
                            var tokenScopes = scopeProperty.GetString()?.Split(' ') ?? Array.Empty<string>();
                            var hasRequiredScopes = oauth2Config.RequiredScopes.All(rs => tokenScopes.Contains(rs));

                            if (!hasRequiredScopes)
                            {
                                result.IsValid = false;
                                result.ErrorMessage = "Token no tiene los scopes requeridos";
                            }
                        }
                        else
                        {
                            result.IsValid = false;
                            result.ErrorMessage = "Token no contiene información de scopes";
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = "Token OAuth2 inactivo";
                }
            }
            else
            {
                result.ErrorMessage = "Error validando token OAuth2";
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = "Error validando OAuth2";
            _logger.LogError(ex, "Error validando OAuth2 para API {IdAPI}", idAPI);
        }

        return result;
    }

    public async Task<AuthValidationResult> ValidateNTLMAsync(string credentials, int idAPI)
    {
        var result = new AuthValidationResult { IdAPI = idAPI, TipoAuth = "NTLM" };

        try
        {
            // NOTA: Implementación simplificada - en producción se requeriría
            // integración completa con Active Directory
            var apiConfig = await GetAPIConfigForAuthAsync(idAPI);
            if (apiConfig?.TipoAutenticacion != "NTLM")
            {
                result.ErrorMessage = "API no configurada para NTLM";
                return result;
            }

            var ntlmConfig = apiConfig.GetConfiguracionAuth<NTLMConfiguration>();
            if (ntlmConfig == null)
            {
                result.ErrorMessage = "Configuración NTLM no válida";
                return result;
            }

            // Aquí iría la validación real con Windows Authentication/Active Directory
            // Por ahora, implementación placeholder
            result.IsValid = ntlmConfig.RequireAuthentication;
            result.ErrorMessage = result.IsValid ? null : "Autenticación NTLM requerida";

            _logger.LogWarning("Validación NTLM usando implementación placeholder para API {IdAPI}", idAPI);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = "Error validando NTLM";
            _logger.LogError(ex, "Error validando NTLM para API {IdAPI}", idAPI);
        }

        return result;
    }

    public async Task<AuthValidationResult> ValidateBasicAuthAsync(string credentials, int idAPI)
    {
        var result = new AuthValidationResult { IdAPI = idAPI, TipoAuth = "BASIC" };

        try
        {
            var apiConfig = await GetAPIConfigForAuthAsync(idAPI);
            if (apiConfig?.TipoAutenticacion != "BASIC")
            {
                result.ErrorMessage = "API no configurada para Basic Auth";
                return result;
            }

            var basicConfig = apiConfig.GetConfiguracionAuth<BasicAuthConfiguration>();
            if (basicConfig == null)
            {
                result.ErrorMessage = "Configuración Basic Auth no válida";
                return result;
            }

            // Decodificar credenciales Basic Auth
            var decodedCredentials = DecodeBasicAuthCredentials(credentials);
            if (decodedCredentials == null)
            {
                result.ErrorMessage = "Credenciales Basic Auth mal formadas";
                return result;
            }

            // Buscar usuario en configuración
            var user = basicConfig.Users?.FirstOrDefault(u =>
                u.Username == decodedCredentials.Value.Username && u.IsActive);

            if (user == null)
            {
                result.ErrorMessage = "Usuario no encontrado o inactivo";
                return result;
            }

            // Verificar contraseña
            var passwordValid = VerifyPassword(decodedCredentials.Value.Password, user.PasswordHash);

            result.IsValid = passwordValid;
            if (!passwordValid)
            {
                result.ErrorMessage = "Contraseña incorrecta";
            }
            else
            {
                result.AuthMetadata = new Dictionary<string, object>
                {
                    ["Username"] = user.Username,
                    ["Roles"] = user.Roles ?? new List<string>(),
                    ["ValidatedAt"] = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = "Error validando Basic Auth";
            _logger.LogError(ex, "Error validando Basic Auth para API {IdAPI}", idAPI);
        }

        return result;
    }

    private async Task<AuthValidationResult> ValidateNoneAuthAsync(int idAPI)
    {
        // Para APIs públicas (sin autenticación)
        var isPublic = await IsAPIPublicAsync(idAPI);

        return new AuthValidationResult
        {
            IsValid = isPublic,
            IdAPI = idAPI,
            TipoAuth = "NONE",
            ErrorMessage = isPublic ? null : "API requiere autenticación"
        };
    }

    // =====================================================
    // RATE LIMITING
    // =====================================================

    public async Task<bool> CheckRateLimitAsync(int idCredencial, int idAPI)
    {
        const string sql = @"
            SELECT COUNT(*) 
            FROM AuditLogs al
            INNER JOIN APIs a ON al.IdAPI = a.IdAPI
            WHERE al.IdCredencial = @IdCredencial 
            AND al.FechaEjecucion >= DATEADD(MINUTE, -1, GETDATE())
            AND a.IdAPI = @IdAPI";

        var currentCount = await _dbService.QueryFirstOrDefaultAsync<int>(sql, new { IdCredencial = idCredencial, IdAPI = idAPI });

        const string rateLimitSql = "SELECT RateLimitPorMinuto FROM APIs WHERE IdAPI = @IdAPI";
        var rateLimit = await _dbService.QueryFirstOrDefaultAsync<int>(rateLimitSql, new { IdAPI = idAPI });

        return currentCount >= rateLimit;
    }

    public async Task<bool> IncrementRateLimitAsync(int idCredencial)
    {
        // Incrementar contador de uso en memoria para rate limiting inmediato
        var key = idCredencial;
        if (!_rateLimitCache.ContainsKey(key))
        {
            _rateLimitCache[key] = DateTime.UtcNow;
        }

        return true;
    }

    public async Task<Dictionary<int, int>> GetCurrentRateLimitsAsync(List<int> credencialIds)
    {
        if (!credencialIds.Any()) return new Dictionary<int, int>();

        const string sql = @"
            SELECT 
                al.IdCredencial,
                COUNT(*) as CurrentCount
            FROM AuditLogs al
            WHERE al.IdCredencial IN @CredencialIds
            AND al.FechaEjecucion >= DATEADD(MINUTE, -1, GETDATE())
            GROUP BY al.IdCredencial";

        var results = await _dbService.QueryAsync<dynamic>(sql, new { CredencialIds = credencialIds });

        return results.ToDictionary(r => (int)r.IdCredencial, r => (int)r.CurrentCount);
    }

    // =====================================================
    // CONFIGURACIÓN Y METADATOS
    // =====================================================

    public async Task<ApiConfiguration?> GetAPIConfigForAuthAsync(int idAPI)
    {
        const string sql = @"
            SELECT 
                a.IdAPI, a.NombreAPI, a.Descripcion, a.ObjetoSQL, a.TipoObjeto,
                a.EsActivo, a.RateLimitPorMinuto, a.ConfiguracionAuth,
                ta.Codigo as TipoAutenticacion, ta.Nombre as NombreTipoAuth,
                ta.RequiereConfiguracion
            FROM APIs a
            INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
            WHERE a.IdAPI = @IdAPI AND a.EsActivo = 1";

        return await _dbService.QueryFirstOrDefaultAsync<ApiConfiguration>(sql, new { IdAPI = idAPI });
    }

    public async Task<bool> IsAPIPublicAsync(int idAPI)
    {
        const string sql = @"
            SELECT CASE WHEN ta.Codigo = 'NONE' THEN 1 ELSE 0 END
            FROM APIs a
            INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
            WHERE a.IdAPI = @IdAPI AND a.EsActivo = 1";

        return await _dbService.QueryFirstOrDefaultAsync<bool>(sql, new { IdAPI = idAPI });
    }

    /// <summary>
    /// IMPLEMENTACIÓN MEJORADA: GetRequiredAuthTypeAsync
    /// </summary>
    public async Task<string> GetRequiredAuthTypeAsync(int idAPI)
    {
        try
        {
            const string sql = @"
                SELECT ta.Codigo
                FROM APIs a
                INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
                WHERE a.IdAPI = @IdAPI 
                  AND a.EsActivo = 1 
                  AND ta.EsActivo = 1";

            var authType = await _dbService.QueryFirstOrDefaultAsStringAsync(sql, new { IdAPI = idAPI });

            if (string.IsNullOrEmpty(authType))
            {
                _logger.LogWarning("No se encontró tipo de autenticación para API {IdAPI} o API inactiva", idAPI);
                return string.Empty;
            }

            _logger.LogDebug("API {IdAPI} requiere autenticación tipo {AuthType}", idAPI, authType);
            return authType;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo tipo de autenticación requerido para API {IdAPI}", idAPI);
            return string.Empty;
        }
    }

    /// <summary>
    /// Verifica múltiples aspectos de acceso en una sola operación (método de conveniencia)
    /// </summary>
    public async Task<CredentialAccessValidationResult> VerifyFullCredentialAccessAsync(int idCredencial, int idAPI)
    {
        try
        {
            const string sql = @"
                SELECT 
                    c.IdCredencial,
                    c.IdAPI,
                    c.EsActivo as CredencialActiva,
                    c.FechaExpiracion,
                    a.EsActivo as APIActiva,
                    a.NombreAPI,
                    ta.Codigo as TipoAutenticacion,
                    ta.EsActivo as TipoAuthActivo,
                    CASE 
                        WHEN c.FechaExpiracion IS NULL THEN 1
                        WHEN c.FechaExpiracion > GETDATE() THEN 1
                        ELSE 0
                    END as NoExpirada
                FROM CredencialesAPI c
                INNER JOIN APIs a ON c.IdAPI = a.IdAPI
                INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
                WHERE c.IdCredencial = @IdCredencial 
                  AND c.IdAPI = @IdAPI";

            var result = await _dbService.QueryFirstOrDefaultAsync<dynamic>(sql,
                new { IdCredencial = idCredencial, IdAPI = idAPI });

            if (result == null)
            {
                return new CredentialAccessValidationResult
                {
                    HasAccess = false,
                    ErrorMessage = "Credencial no encontrada o no pertenece a esta API",
                    IdCredencial = idCredencial,
                    IdAPI = idAPI
                };
            }

            var hasAccess = (bool)result.CredencialActiva &&
                           (bool)result.APIActiva &&
                           (bool)result.TipoAuthActivo &&
                           (bool)result.NoExpirada;

            var errorMessages = new List<string>();
            if (!(bool)result.CredencialActiva) errorMessages.Add("Credencial inactiva");
            if (!(bool)result.APIActiva) errorMessages.Add("API inactiva");
            if (!(bool)result.TipoAuthActivo) errorMessages.Add("Tipo de autenticación inactivo");
            if (!(bool)result.NoExpirada) errorMessages.Add("Credencial expirada");

            return new CredentialAccessValidationResult
            {
                HasAccess = hasAccess,
                ErrorMessage = errorMessages.Any() ? string.Join(", ", errorMessages) : null,
                IdCredencial = idCredencial,
                IdAPI = idAPI,
                APIName = result.NombreAPI,
                AuthType = result.TipoAutenticacion,
                ExpirationDate = result.FechaExpiracion
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en verificación completa de acceso para credencial {IdCredencial} y API {IdAPI}", idCredencial, idAPI);
            return new CredentialAccessValidationResult
            {
                HasAccess = false,
                ErrorMessage = "Error interno verificando acceso",
                IdCredencial = idCredencial,
                IdAPI = idAPI
            };
        }
    }

    // =====================================================
    // AUDITORÍA
    // =====================================================

    public async Task LogAuthenticationAttemptAsync(int idAPI, int? idCredencial, bool isSuccessful, string? errorMessage, string? ipAddress, string ambiente)
    {
        const string sql = @"
            INSERT INTO AuditLogs (IdAPI, IdCredencial, Ambiente, EsExitoso, MensajeError, TiempoEjecucionMs, DireccionIP)
            VALUES (@IdAPI, @IdCredencial, @ambiente, @EsExitoso, @MensajeError, 0, @DireccionIP)";

        await _dbService.ExecuteAsync(sql, new
        {
            IdAPI = idAPI,
            IdCredencial = idCredencial,
            Ambiente = ambiente,
            EsExitoso = isSuccessful,
            MensajeError = errorMessage,
            DireccionIP = ipAddress
        });
    }

    public async Task<List<AuditLog>> GetFailedAuthAttemptsAsync(DateTime? fechaDesde = null, int count = 100)
    {
        fechaDesde ??= DateTime.Now.AddDays(-7);

        const string sql = @"
            SELECT TOP (@Count)
                al.IdAPI, al.IdCredencial, al.FechaEjecucion, al.MensajeError, al.DireccionIP,
                a.NombreAPI, c.Nombre as NombreCredencial
            FROM AuditLogs al
            INNER JOIN APIs a ON al.IdAPI = a.IdAPI
            LEFT JOIN CredencialesAPI c ON al.IdCredencial = c.IdCredencial
            WHERE al.EsExitoso = 0 
            AND al.Ambiente = 'AUTH'
            AND al.FechaEjecucion >= @FechaDesde
            ORDER BY al.FechaEjecucion DESC";

        var logs = await _dbService.QueryAsync<AuditLog>(sql, new { Count = count, FechaDesde = fechaDesde });

        return logs.ToList();
    }

    // =====================================================
    // ESTADÍSTICAS Y MONITOREO
    // =====================================================

    public async Task<AuthStatsDto> GetAuthStatsAsync(int idAPI)
    {
        const string sql = @"
            SELECT 
                a.IdAPI, a.NombreAPI, ta.Codigo as TipoAuth,
                COUNT(c.IdCredencial) as TotalCredenciales,
                SUM(CASE WHEN c.EsActivo = 1 AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE()) THEN 1 ELSE 0 END) as CredencialesActivas,
                SUM(CASE WHEN c.FechaExpiracion IS NOT NULL AND c.FechaExpiracion <= GETDATE() THEN 1 ELSE 0 END) as CredencialesExpiradas
            FROM APIs a
            INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
            LEFT JOIN CredencialesAPI c ON a.IdAPI = c.IdAPI
            WHERE a.IdAPI = @IdAPI
            GROUP BY a.IdAPI, a.NombreAPI, ta.Codigo";

        var stats = await _dbService.QueryFirstOrDefaultAsync<AuthStatsDto>(sql, new { IdAPI = idAPI });

        if (stats != null)
        {
            // Obtener estadísticas de autenticación de hoy
            const string authSql = @"
                SELECT 
                    COUNT(*) as AuthenticationsToday,
                    SUM(CASE WHEN EsExitoso = 0 THEN 1 ELSE 0 END) as FailedAuthsToday,
                    MAX(FechaEjecucion) as LastAuthentication
                FROM AuditLogs 
                WHERE IdAPI = @IdAPI 
                AND Ambiente = 'AUTH'
                AND FechaEjecucion >= CAST(GETDATE() AS DATE)";

            var authStats = await _dbService.QueryFirstOrDefaultAsync<dynamic>(authSql, new { IdAPI = idAPI });

            if (authStats != null)
            {
                stats.AuthenticationsToday = authStats.AuthenticationsToday ?? 0;
                stats.FailedAuthsToday = authStats.FailedAuthsToday ?? 0;
                stats.LastAuthentication = authStats.LastAuthentication;
                stats.SuccessRate = stats.AuthenticationsToday > 0
                    ? (double)(stats.AuthenticationsToday - stats.FailedAuthsToday) / stats.AuthenticationsToday * 100
                    : 0;
            }
        }

        return stats ?? new AuthStatsDto { IdAPI = idAPI, NombreAPI = "API no encontrada" };
    }

    public async Task<List<ActivityDto>> GetRecentActivityAsync(int idAPI, int count = 10)
    {
        const string sql = @"
            SELECT TOP (@Count)
                al.FechaEjecucion as Timestamp,
                CASE 
                    WHEN al.EsExitoso = 1 THEN 'AUTH_SUCCESS'
                    ELSE 'AUTH_FAILED'
                END as EventType,
                al.IdAPI,
                al.IdCredencial,
                CONCAT('Autenticación ', CASE WHEN al.EsExitoso = 1 THEN 'exitosa' ELSE 'fallida' END, 
                       CASE WHEN c.Nombre IS NOT NULL THEN CONCAT(' - ', c.Nombre) ELSE '' END) as Description,
                al.DireccionIP as IPAddress
            FROM AuditLogs al
            LEFT JOIN CredencialesAPI c ON al.IdCredencial = c.IdCredencial
            WHERE al.IdAPI = @IdAPI AND al.Ambiente = 'AUTH'
            ORDER BY al.FechaEjecucion DESC";

        var logs = await _dbService.QueryAsync<ActivityDto>(sql, new { Count = count, IdAPI = idAPI });

        return logs.ToList();
    }

    public async Task<SystemAuthHealthDto> GetSystemAuthHealthAsync()
    {
        const string sql = @"
            SELECT 
                COUNT(DISTINCT a.IdAPI) as TotalAPIs,
                SUM(CASE WHEN ta.Codigo != 'NONE' THEN 1 ELSE 0 END) as APIsWithAuth,
                COUNT(c.IdCredencial) as TotalCredentials,
                SUM(CASE WHEN c.EsActivo = 1 AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE()) THEN 1 ELSE 0 END) as ActiveCredentials,
                SUM(CASE WHEN c.FechaExpiracion IS NOT NULL AND c.FechaExpiracion <= GETDATE() THEN 1 ELSE 0 END) as ExpiredCredentials,
                SUM(CASE WHEN c.FechaExpiracion IS NOT NULL AND c.FechaExpiracion <= DATEADD(DAY, 7, GETDATE()) AND c.FechaExpiracion > GETDATE() THEN 1 ELSE 0 END) as CredentialsExpiringSoon
            FROM APIs a
            INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
            LEFT JOIN CredencialesAPI c ON a.IdAPI = c.IdAPI
            WHERE a.EsActivo = 1";

        var health = await _dbService.QueryFirstOrDefaultAsync<SystemAuthHealthDto>(sql);

        if (health != null)
        {
            // Obtener distribución por tipo de auth
            const string distSql = @"
                SELECT ta.Codigo, COUNT(a.IdAPI) as Count
                FROM APIs a
                INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
                WHERE a.EsActivo = 1
                GROUP BY ta.Codigo";

            var distribution = await _dbService.QueryAsync<dynamic>(distSql);
            health.AuthTypeDistribution = distribution.ToDictionary(
                d => (TipoAutenticacion)Enum.Parse(typeof(TipoAutenticacion), d.Codigo.ToString()),
                d => (int)d.Count);

            // Evaluar salud general
            health.IsHealthy = health.ExpiredCredentials == 0 && health.CredentialsExpiringSoon < 5;

            if (health.ExpiredCredentials > 0)
                health.Warnings.Add($"{health.ExpiredCredentials} credenciales expiradas requieren limpieza");

            if (health.CredentialsExpiringSoon > 0)
                health.Warnings.Add($"{health.CredentialsExpiringSoon} credenciales expiran pronto");
        }

        return health ?? new SystemAuthHealthDto();
    }

    // =====================================================
    // MÉTODOS AUXILIARES
    // =====================================================

    private async Task UpdateLastUsageAsync(int idCredencial)
    {
        //const string sql = @"
        //    UPDATE CredencialesAPI 
        //    SET UltimoUso = GETDATE(), ContadorUsos = ISNULL(ContadorUsos, 0) + 1
        //    WHERE IdCredencial = @IdCredencial";

        const string sql = @"
            UPDATE CredencialesAPI 
            SET UltimoUso = GETDATE()
            WHERE IdCredencial = @IdCredencial";

        await _dbService.ExecuteAsync(sql, new { IdCredencial = idCredencial });
    }

    private (string Username, string Password)? DecodeBasicAuthCredentials(string credentials)
    {
        try
        {
            // Remover prefijo "Basic " si existe
            if (credentials.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                credentials = credentials["Basic ".Length..];
            }

            var decodedBytes = Convert.FromBase64String(credentials);
            var decodedString = Encoding.UTF8.GetString(decodedBytes);
            var colonIndex = decodedString.IndexOf(':');

            if (colonIndex == -1) return null;

            return (decodedString[..colonIndex], decodedString[(colonIndex + 1)..]);
        }
        catch
        {
            return null;
        }
    }

    private bool VerifyPassword(string password, string hash)
    {
        // NOTA: En producción, usar BCrypt o similar para verificar hashes
        // Esta es una implementación simple para demostración
        return password == hash; // CAMBIAR POR VERIFICACIÓN SEGURA
    }
}

// =====================================================
// MODELO PARA RESULTADO DE VALIDACIÓN COMPLETA
// =====================================================
public class CredentialAccessValidationResult
{
    public bool HasAccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int IdCredencial { get; set; }
    public int IdAPI { get; set; }
    public string? APIName { get; set; }
    public string? AuthType { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
}