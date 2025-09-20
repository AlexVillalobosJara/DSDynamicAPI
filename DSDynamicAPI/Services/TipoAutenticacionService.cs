// Services/Implementation/TipoAutenticacionService.cs
using DynamicAPIs.Services.Database;
using DynamicAPIs.Services.Interfaces;
using System.Text.Json;

namespace DynamicAPIs.Services.Implementation;

public class TipoAutenticacionService : ITipoAutenticacionService
{
    private readonly DatabaseService _dbService;
    private readonly ILogger<TipoAutenticacionService> _logger;

    public TipoAutenticacionService(DatabaseService dbService, ILogger<TipoAutenticacionService> logger)
    {
        _dbService = dbService;
        _logger = logger;
    }

    // =====================================================
    // GESTIÓN DE TIPOS DE AUTENTICACIÓN
    // =====================================================

    public async Task<List<TipoAutenticacionDto>> GetAllTiposAsync()
    {
        try
        {
            const string sql = @"
                SELECT IdTipoAuth, Codigo, Nombre, Descripcion, RequiereConfiguracion, EsActivo, FechaCreacion
                FROM TiposAutenticacion 
                ORDER BY Nombre";

            var tipos = await _dbService.QueryAsync<TipoAutenticacionDto>(sql);

            _logger.LogInformation("Obtenidos {Count} tipos de autenticación", tipos.Count());
            return tipos.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo todos los tipos de autenticación");
            throw;
        }
    }

    public async Task<List<TipoAutenticacionDto>> GetActiveTiposAsync()
    {
        try
        {
            const string sql = @"
                SELECT IdTipoAuth, Codigo, Nombre, Descripcion, RequiereConfiguracion, EsActivo, FechaCreacion
                FROM TiposAutenticacion 
                WHERE EsActivo = 1
                ORDER BY Nombre";

            var tipos = await _dbService.QueryAsync<TipoAutenticacionDto>(sql);

            _logger.LogInformation("Obtenidos {Count} tipos de autenticación activos", tipos.Count());
            return tipos.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo tipos de autenticación activos");
            throw;
        }
    }

    public async Task<TipoAutenticacionDto?> GetTipoByIdAsync(int idTipoAuth)
    {
        try
        {
            const string sql = @"
                SELECT IdTipoAuth, Codigo, Nombre, Descripcion, RequiereConfiguracion, EsActivo, FechaCreacion
                FROM TiposAutenticacion 
                WHERE IdTipoAuth = @IdTipoAuth";

            var tipo = await _dbService.QueryFirstOrDefaultAsync<TipoAutenticacionDto>(sql, new { IdTipoAuth = idTipoAuth });

            if (tipo != null)
            {
                _logger.LogInformation("Encontrado tipo de autenticación {IdTipoAuth}: {Codigo}", idTipoAuth, tipo.Codigo);
            }
            else
            {
                _logger.LogWarning("No se encontró tipo de autenticación con ID {IdTipoAuth}", idTipoAuth);
            }

            return tipo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo tipo de autenticación por ID {IdTipoAuth}", idTipoAuth);
            throw;
        }
    }

    public async Task<TipoAutenticacionDto?> GetTipoByCodigoAsync(string codigo)
    {
        try
        {
            const string sql = @"
                SELECT IdTipoAuth, Codigo, Nombre, Descripcion, RequiereConfiguracion, EsActivo, FechaCreacion
                FROM TiposAutenticacion 
                WHERE Codigo = @Codigo";

            var tipo = await _dbService.QueryFirstOrDefaultAsync<TipoAutenticacionDto>(sql, new { Codigo = codigo });

            if (tipo != null)
            {
                _logger.LogInformation("Encontrado tipo de autenticación por código {Codigo}: {Nombre}", codigo, tipo.Nombre);
            }
            else
            {
                _logger.LogWarning("No se encontró tipo de autenticación con código {Codigo}", codigo);
            }

            return tipo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo tipo de autenticación por código {Codigo}", codigo);
            throw;
        }
    }

    // =====================================================
    // VALIDACIÓN DE CONFIGURACIONES
    // =====================================================

    public async Task<bool> ValidateConfigurationAsync(TipoAutenticacion tipo, string? configuracion)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(configuracion))
            {
                // Si no requiere configuración, es válido tener null
                var tipoInfo = await GetTipoByCodigoAsync(tipo.ToString());
                return tipoInfo?.RequiereConfiguracion != true;
            }

            // Validar según el tipo de autenticación
            return tipo switch
            {
                TipoAutenticacion.JWT => await ValidateJWTConfigurationAsync(configuracion),
                TipoAutenticacion.OAUTH2 => await ValidateOAuth2ConfigurationAsync(configuracion),
                TipoAutenticacion.NTLM => await ValidateNTLMConfigurationAsync(configuracion),
                TipoAutenticacion.BASIC => await ValidateBasicAuthConfigurationAsync(configuracion),
                TipoAutenticacion.TOKEN => true, // Token no requiere configuración especial
                TipoAutenticacion.APIKEY => true, // API Key no requiere configuración especial
                TipoAutenticacion.NONE => true, // Sin autenticación
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando configuración para tipo {TipoAuth}", tipo);
            return false;
        }
    }

    public async Task<string?> GetDefaultConfigurationAsync(TipoAutenticacion tipo)
    {
        try
        {
            return tipo switch
            {
                TipoAutenticacion.JWT => GetDefaultJWTConfiguration(),
                TipoAutenticacion.OAUTH2 => GetDefaultOAuth2Configuration(),
                TipoAutenticacion.NTLM => GetDefaultNTLMConfiguration(),
                TipoAutenticacion.BASIC => GetDefaultBasicAuthConfiguration(),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo configuración por defecto para tipo {TipoAuth}", tipo);
            return null;
        }
    }

    // =====================================================
    // MÉTODOS DE VALIDACIÓN ESPECÍFICOS
    // =====================================================

    private async Task<bool> ValidateJWTConfigurationAsync(string configuracion)
    {
        try
        {
            var config = JsonSerializer.Deserialize<JWTConfiguration>(configuracion);

            if (config == null) return false;

            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(config.Issuer) ||
                string.IsNullOrWhiteSpace(config.Audience) ||
                string.IsNullOrWhiteSpace(config.SecretKey))
            {
                return false;
            }

            // Validar longitud mínima de la clave secreta
            if (config.SecretKey.Length < 32)
            {
                _logger.LogWarning("Clave secreta JWT demasiado corta: {Length} caracteres", config.SecretKey.Length);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando configuración JWT");
            return false;
        }
    }

    private async Task<bool> ValidateOAuth2ConfigurationAsync(string configuracion)
    {
        try
        {
            var config = JsonSerializer.Deserialize<OAuth2Configuration>(configuracion);

            if (config == null) return false;

            // Validaciones básicas
            if (string.IsNullOrWhiteSpace(config.AuthorizationServer) ||
                string.IsNullOrWhiteSpace(config.TokenEndpoint) ||
                string.IsNullOrWhiteSpace(config.ClientId) ||
                string.IsNullOrWhiteSpace(config.ClientSecret))
            {
                return false;
            }

            // Validar URLs
            if (!Uri.TryCreate(config.AuthorizationServer, UriKind.Absolute, out _) ||
                !Uri.TryCreate(config.TokenEndpoint, UriKind.Absolute, out _))
            {
                _logger.LogWarning("URLs inválidas en configuración OAuth2");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando configuración OAuth2");
            return false;
        }
    }

    private async Task<bool> ValidateNTLMConfigurationAsync(string configuracion)
    {
        try
        {
            var config = JsonSerializer.Deserialize<NTLMConfiguration>(configuracion);

            if (config == null) return false;

            // Para NTLM, el dominio es opcional pero recomendado
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando configuración NTLM");
            return false;
        }
    }

    private async Task<bool> ValidateBasicAuthConfigurationAsync(string configuracion)
    {
        try
        {
            var config = JsonSerializer.Deserialize<BasicAuthConfiguration>(configuracion);

            if (config == null) return false;

            // Debe tener al menos un usuario
            if (!config.Users.Any())
            {
                _logger.LogWarning("Configuración Basic Auth sin usuarios");
                return false;
            }

            // Validar que todos los usuarios tengan username y password hash
            foreach (var user in config.Users)
            {
                if (string.IsNullOrWhiteSpace(user.Username) ||
                    string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    _logger.LogWarning("Usuario Basic Auth incompleto: {Username}", user.Username);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando configuración Basic Auth");
            return false;
        }
    }

    // =====================================================
    // CONFIGURACIONES POR DEFECTO
    // =====================================================

    private static string GetDefaultJWTConfiguration()
    {
        var defaultConfig = new JWTConfiguration
        {
            Issuer = "https://your-auth-server.com",
            Audience = "your-api-audience",
            SecretKey = "your-secret-key-must-be-at-least-32-characters-long",
            ValidateLifetime = true,
            ClockSkewSeconds = 300,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true
        };

        return JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string GetDefaultOAuth2Configuration()
    {
        var defaultConfig = new OAuth2Configuration
        {
            AuthorizationServer = "https://your-oauth-server.com",
            TokenEndpoint = "https://your-oauth-server.com/token",
            IntrospectionEndpoint = "https://your-oauth-server.com/introspect",
            ClientId = "your-client-id",
            ClientSecret = "your-client-secret",
            RequiredScopes = new List<string> { "api:read", "api:write" },
            TokenCacheMinutes = 5
        };

        return JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string GetDefaultNTLMConfiguration()
    {
        var defaultConfig = new NTLMConfiguration
        {
            Domain = "YOUR-DOMAIN",
            RequiredGroups = new List<string> { "API-Users", "Developers" },
            AllowedUsers = new List<string> { "user1@yourdomain.com", "DOMAIN\\user2" },
            RequireAuthentication = true
        };

        return JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static string GetDefaultBasicAuthConfiguration()
    {
        var defaultConfig = new BasicAuthConfiguration
        {
            Users = new List<BasicAuthUser>
            {
                new()
                {
                    Username = "api-user",
                    PasswordHash = "hash-of-your-password-use-bcrypt-or-similar",
                    Roles = new List<string> { "read", "write" },
                    IsActive = true
                }
            },
            RequireHttps = true,
            Realm = "Dynamic API"
        };

        return JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    // =====================================================
    // MÉTODOS DE UTILIDAD
    // =====================================================

    public async Task<Dictionary<string, string>> GetAllDefaultConfigurationsAsync()
    {
        var configurations = new Dictionary<string, string>();
        var tiposActivos = await GetActiveTiposAsync();

        foreach (var tipo in tiposActivos.Where(t => t.RequiereConfiguracion))
        {
            if (Enum.TryParse<TipoAutenticacion>(tipo.Codigo, out var tipoEnum))
            {
                var defaultConfig = await GetDefaultConfigurationAsync(tipoEnum);
                if (!string.IsNullOrEmpty(defaultConfig))
                {
                    configurations[tipo.Codigo] = defaultConfig;
                }
            }
        }

        return configurations;
    }

    public async Task<bool> IsValidAuthTypeAsync(string codigo)
    {
        var tipo = await GetTipoByCodigoAsync(codigo);
        return tipo?.EsActivo == true;
    }

    public async Task<List<string>> GetRequiredConfigurationFieldsAsync(TipoAutenticacion tipo)
    {
        return tipo switch
        {
            TipoAutenticacion.JWT => new List<string> { "Issuer", "Audience", "SecretKey" },
            TipoAutenticacion.OAUTH2 => new List<string> { "AuthorizationServer", "TokenEndpoint", "ClientId", "ClientSecret" },
            TipoAutenticacion.NTLM => new List<string> { "Domain" },
            TipoAutenticacion.BASIC => new List<string> { "Users", "Realm" },
            _ => new List<string>()
        };
    }

    public async Task<string?> GetConfigurationSchemaAsync(TipoAutenticacion tipo)
    {
        // Retorna un JSON Schema básico para validación en frontend
        return tipo switch
        {
            TipoAutenticacion.JWT => GetJWTSchema(),
            TipoAutenticacion.OAUTH2 => GetOAuth2Schema(),
            TipoAutenticacion.NTLM => GetNTLMSchema(),
            TipoAutenticacion.BASIC => GetBasicAuthSchema(),
            _ => null
        };
    }

    // =====================================================
    // SCHEMAS PARA VALIDACIÓN EN FRONTEND
    // =====================================================

    private static string GetJWTSchema()
    {
        return @"{
            ""type"": ""object"",
            ""properties"": {
                ""Issuer"": { ""type"": ""string"", ""minLength"": 1 },
                ""Audience"": { ""type"": ""string"", ""minLength"": 1 },
                ""SecretKey"": { ""type"": ""string"", ""minLength"": 32 },
                ""ValidateLifetime"": { ""type"": ""boolean"" },
                ""ClockSkewSeconds"": { ""type"": ""integer"", ""minimum"": 0, ""maximum"": 3600 },
                ""ValidateIssuer"": { ""type"": ""boolean"" },
                ""ValidateAudience"": { ""type"": ""boolean"" },
                ""ValidateIssuerSigningKey"": { ""type"": ""boolean"" }
            },
            ""required"": [""Issuer"", ""Audience"", ""SecretKey""]
        }";
    }

    private static string GetOAuth2Schema()
    {
        return @"{
            ""type"": ""object"",
            ""properties"": {
                ""AuthorizationServer"": { ""type"": ""string"", ""format"": ""uri"" },
                ""TokenEndpoint"": { ""type"": ""string"", ""format"": ""uri"" },
                ""IntrospectionEndpoint"": { ""type"": ""string"", ""format"": ""uri"" },
                ""ClientId"": { ""type"": ""string"", ""minLength"": 1 },
                ""ClientSecret"": { ""type"": ""string"", ""minLength"": 1 },
                ""RequiredScopes"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                ""TokenCacheMinutes"": { ""type"": ""integer"", ""minimum"": 0, ""maximum"": 60 }
            },
            ""required"": [""AuthorizationServer"", ""TokenEndpoint"", ""ClientId"", ""ClientSecret""]
        }";
    }

    private static string GetNTLMSchema()
    {
        return @"{
            ""type"": ""object"",
            ""properties"": {
                ""Domain"": { ""type"": ""string"", ""minLength"": 1 },
                ""RequiredGroups"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                ""AllowedUsers"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                ""RequireAuthentication"": { ""type"": ""boolean"" }
            },
            ""required"": [""Domain""]
        }";
    }

    private static string GetBasicAuthSchema()
    {
        return @"{
            ""type"": ""object"",
            ""properties"": {
                ""Users"": {
                    ""type"": ""array"",
                    ""items"": {
                        ""type"": ""object"",
                        ""properties"": {
                            ""Username"": { ""type"": ""string"", ""minLength"": 1 },
                            ""PasswordHash"": { ""type"": ""string"", ""minLength"": 1 },
                            ""Roles"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } },
                            ""IsActive"": { ""type"": ""boolean"" }
                        },
                        ""required"": [""Username"", ""PasswordHash""]
                    }
                },
                ""RequireHttps"": { ""type"": ""boolean"" },
                ""Realm"": { ""type"": ""string"" }
            },
            ""required"": [""Users""]
        }";
    }
}

// =====================================================
// MODELOS DE CONFIGURACIÓN DE AUTENTICACIÓN
// =====================================================

// Configuración JWT
public class JWTConfiguration
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool ValidateLifetime { get; set; } = true;
    public int ClockSkewSeconds { get; set; } = 300;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateIssuerSigningKey { get; set; } = true;
}

// Configuración OAuth2
public class OAuth2Configuration
{
    public string AuthorizationServer { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string? IntrospectionEndpoint { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public List<string> RequiredScopes { get; set; } = new();
    public int TokenCacheMinutes { get; set; } = 5;
}

// Configuración NTLM
public class NTLMConfiguration
{
    public string? Domain { get; set; }
    public List<string> RequiredGroups { get; set; } = new();
    public List<string> AllowedUsers { get; set; } = new();
    public bool RequireAuthentication { get; set; } = true;
}

// Configuración Basic Auth
public class BasicAuthConfiguration
{
    public List<BasicAuthUser> Users { get; set; } = new();
    public bool RequireHttps { get; set; } = true;
    public string Realm { get; set; } = "Dynamic API";
}

public class BasicAuthUser
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty; // Debe ser un hash, no texto plano
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; } = true;
}