// =====================================================
// ConfigurationService - IMPLEMENTACI�N ACTUALIZADA
// =====================================================

using Dapper;
using DSDynamicAPI.Services;
using DynamicAPIs.Services.Database;
using Microsoft.Extensions.Options;
using System.Data;

namespace DynamicAPIs.Services.Implementation;

public class ConfigurationService : IConfigurationService
{
    private readonly DatabaseService _dbService;
    private readonly DatabaseOptions _dbOptions;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(
        DatabaseService dbService,
        IOptions<DatabaseOptions> dbOptions,
        ILogger<ConfigurationService> logger)
    {
        _dbService = dbService;
        _dbOptions = dbOptions.Value;
        _logger = logger;
    }

    // =====================================================
    // M�TODOS PRINCIPALES
    // =====================================================

    public async Task<ApiConfiguration?> GetApiConfigurationAsync(int idApi, string? credential = null)
    {
        try
        {
            _logger.LogInformation("Obteniendo configuraci�n para API {IdAPI} con credencial proporcionada: {HasCredential}",
                idApi, !string.IsNullOrEmpty(credential));

            // Usar stored procedure actualizado
            var parameters = new DynamicParameters();
            parameters.Add("@IdAPI", idApi);
            parameters.Add("@ValorCredencial", credential);

            using var connection = await _dbService.GetConnectionAsync();

            // Ejecutar SP que valida credencial y retorna configuraci�n
            var results = await connection.QueryMultipleAsync(
                "sp_GetAPIConfigV2",
                parameters,
                commandType: CommandType.StoredProcedure,
                commandTimeout: _dbOptions.DefaultCommandTimeout);

            // Primera consulta: configuraci�n de la API
            var apiConfig = await results.ReadFirstOrDefaultAsync<ApiConfiguration>();
            if (apiConfig == null)
            {
                _logger.LogWarning("API {IdAPI} no encontrada o credencial inv�lida", idApi);
                return null;
            }

            // Segunda consulta: par�metros
            var parametros = (await results.ReadAsync<ApiParameter>()).ToList();
            apiConfig.Parametros = parametros;

            _logger.LogInformation("Configuraci�n obtenida exitosamente para API {IdAPI}: {NombreAPI}",
                idApi, apiConfig.NombreAPI);

            return apiConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo configuraci�n para API {IdAPI}", idApi);
            throw;
        }
    }

    public async Task<List<ApiInfo>> GetAvailableApisAsync()
    {
        try
        {
            const string sql = @"
                SELECT 
                    a.IdAPI,
                    a.NombreAPI,
                    a.Descripcion,
                    a.TipoObjeto,
                    ta.Codigo as TipoAutenticacion,
                    ta.Nombre as NombreTipoAuth,
                    ta.RequiereConfiguracion
                FROM APIs a
                INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
                WHERE a.EsActivo = 1
                ORDER BY a.NombreAPI";

            var apis = await _dbService.QueryAsync<dynamic>(sql);

            var result = new List<ApiInfo>();

            foreach (var api in apis)
            {
                // Obtener par�metros para cada API
                const string paramSql = @"
                    SELECT NombreParametro, TipoParametro, EsObligatorio, ValorPorDefecto, Descripcion
                    FROM Parametros 
                    WHERE IdAPI = @IdAPI 
                    ORDER BY Orden";

                var parametros = await _dbService.QueryAsync<ParameterInfo>(paramSql, new { IdAPI = api.IdAPI });

                var apiInfo = new ApiInfo
                {
                    IdAPI = api.IdAPI,
                    NombreAPI = api.NombreAPI,
                    Descripcion = api.Descripcion ?? string.Empty,
                    TipoObjeto = api.TipoObjeto,
                    Parametros = parametros.ToList(),
                    Endpoint = $"/api/execute?idApi={api.IdAPI}",
                    ExampleCall = GenerateExampleCall(api.IdAPI, parametros.ToList())
                };

                result.Add(apiInfo);
            }

            _logger.LogInformation("Obtenidas {Count} APIs disponibles", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo APIs disponibles");
            throw;
        }
    }

    // =====================================================
    // NUEVOS M�TODOS PARA SISTEMA MULTI-AUTH
    // =====================================================

    public async Task<ApiConfiguration?> GetApiConfigurationWithAuthAsync(int idApi, TipoAutenticacion tipoAuth, string? credential = null)
    {
        try
        {
            // Verificar que la API est� configurada para el tipo de auth especificado
            const string authCheckSql = @"
                SELECT ta.Codigo
                FROM APIs a
                INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
                WHERE a.IdAPI = @IdAPI AND a.EsActivo = 1";

            var configuredAuthType = await _dbService.QueryFirstOrDefaultAsync<string>(authCheckSql, new { IdAPI = idApi });

            if (configuredAuthType != tipoAuth.ToString())
            {
                _logger.LogWarning("API {IdAPI} configurada para {ConfiguredAuth}, solicitado {RequestedAuth}",
                    idApi, configuredAuthType, tipoAuth);
                return null;
            }

            // Si el tipo coincide, obtener configuraci�n normal
            return await GetApiConfigurationAsync(idApi, credential);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo configuraci�n con auth espec�fico para API {IdAPI}", idApi);
            throw;
        }
    }

    public async Task<List<TipoAutenticacionDto>> GetSupportedAuthTypesAsync()
    {
        try
        {
            const string sql = @"
                SELECT IdTipoAuth, Codigo, Nombre, Descripcion, RequiereConfiguracion, EsActivo, FechaCreacion
                FROM TiposAutenticacion 
                WHERE EsActivo = 1
                ORDER BY Nombre";

            var tipos = await _dbService.QueryAsync<TipoAutenticacionDto>(sql);

            _logger.LogInformation("Obtenidos {Count} tipos de autenticaci�n soportados", tipos.Count());
            return tipos.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo tipos de autenticaci�n soportados");
            throw;
        }
    }

    public async Task<bool> ValidateAPIAuthConfigAsync(int idApi, TipoAutenticacion tipoAuth)
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM APIs a
                INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
                WHERE a.IdAPI = @IdAPI 
                AND ta.Codigo = @TipoAuth 
                AND a.EsActivo = 1 
                AND ta.EsActivo = 1";

            var count = await _dbService.QueryFirstOrDefaultAsync<int>(sql, new { IdAPI = idApi, TipoAuth = tipoAuth.ToString() });

            var isValid = count > 0;
            _logger.LogInformation("Validaci�n de configuraci�n auth para API {IdAPI} con tipo {TipoAuth}: {IsValid}",
                idApi, tipoAuth, isValid);

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando configuraci�n auth para API {IdAPI}", idApi);
            return false;
        }
    }

    public async Task<string?> GetAuthConfigurationAsync(int idApi)
    {
        try
        {
            const string sql = @"
                SELECT ConfiguracionAuth
                FROM APIs 
                WHERE IdAPI = @IdAPI AND EsActivo = 1";

            var config = await _dbService.QueryFirstOrDefaultAsync<string>(sql, new { IdAPI = idApi });

            _logger.LogInformation("Configuraci�n auth obtenida para API {IdAPI}: {HasConfig}",
                idApi, !string.IsNullOrEmpty(config));

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo configuraci�n auth para API {IdAPI}", idApi);
            return null;
        }
    }

    // =====================================================
    // M�TODOS DE VALIDACI�N Y SEGURIDAD
    // =====================================================

    public async Task<bool> ValidateCredentialForAPIAsync(string credential, int idApi, TipoAutenticacion expectedType)
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM CredencialesAPI c
                INNER JOIN APIs a ON c.IdAPI = a.IdAPI
                INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
                WHERE c.ValorCredencial = @Credential
                AND c.IdAPI = @IdAPI
                AND ta.Codigo = @TipoAuth
                AND c.EsActivo = 1
                AND a.EsActivo = 1
                AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE())";

            var count = await _dbService.QueryFirstOrDefaultAsync<int>(sql, new
            {
                Credential = credential,
                IdAPI = idApi,
                TipoAuth = expectedType.ToString()
            });

            var isValid = count > 0;
            _logger.LogInformation("Validaci�n de credencial para API {IdAPI}: {IsValid}", idApi, isValid);

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando credencial para API {IdAPI}", idApi);
            return false;
        }
    }

    public async Task<ApiAuthMetadata?> GetApiAuthMetadataAsync(int idApi)
    {
        try
        {
            const string sql = @"
                SELECT 
                    a.IdAPI,
                    a.NombreAPI,
                    ta.Codigo as TipoAutenticacion,
                    ta.Nombre as NombreTipoAuth,
                    ta.RequiereConfiguracion,
                    a.ConfiguracionAuth,
                    a.RateLimitPorMinuto,
                    COUNT(c.IdCredencial) as TotalCredenciales,
                    SUM(CASE WHEN c.EsActivo = 1 AND (c.FechaExpiracion IS NULL OR c.FechaExpiracion > GETDATE()) THEN 1 ELSE 0 END) as CredencialesActivas
                FROM APIs a
                INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
                LEFT JOIN CredencialesAPI c ON a.IdAPI = c.IdAPI
                WHERE a.IdAPI = @IdAPI AND a.EsActivo = 1
                GROUP BY a.IdAPI, a.NombreAPI, ta.Codigo, ta.Nombre, ta.RequiereConfiguracion, a.ConfiguracionAuth, a.RateLimitPorMinuto";

            var metadata = await _dbService.QueryFirstOrDefaultAsync<ApiAuthMetadata>(sql, new { IdAPI = idApi });

            if (metadata != null)
            {
                _logger.LogInformation("Metadatos de auth obtenidos para API {IdAPI}: {TipoAuth} con {Credenciales} credenciales activas",
                    idApi, metadata.TipoAutenticacion, metadata.CredencialesActivas);
            }

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo metadatos de auth para API {IdAPI}", idApi);
            return null;
        }
    }

    // =====================================================
    // M�TODOS DE CONFIGURACI�N DIN�MICA
    // =====================================================

    public async Task<Dictionary<string, object>> GetDynamicConfigAsync(int idApi)
    {
        try
        {
            const string sql = @"
                SELECT 
                    a.ConfiguracionAuth,
                    ta.Codigo as TipoAuth,
                    cs.StringConexionTest,
                    cs.StringConexionProduccion,
                    cs.TimeoutEjecucionSegundos,
                    cs.UrlBaseDinamica
                FROM APIs a
                INNER JOIN TiposAutenticacion ta ON a.IdTipoAuth = ta.IdTipoAuth
                CROSS JOIN ConfiguracionSistema cs
                WHERE a.IdAPI = @IdAPI AND a.EsActivo = 1";

            var config = await _dbService.QueryFirstOrDefaultAsync<dynamic>(sql, new { IdAPI = idApi });

            if (config == null) return new Dictionary<string, object>();

            var result = new Dictionary<string, object>
            {
                ["TipoAuth"] = config.TipoAuth ?? "NONE",
                ["StringConexionTest"] = config.StringConexionTest ?? string.Empty,
                ["StringConexionProduccion"] = config.StringConexionProduccion ?? string.Empty,
                ["TimeoutEjecucionSegundos"] = config.TimeoutEjecucionSegundos ?? 30,
                ["UrlBaseDinamica"] = config.UrlBaseDinamica ?? string.Empty
            };

            if (!string.IsNullOrEmpty(config.ConfiguracionAuth))
            {
                result["ConfiguracionAuth"] = config.ConfiguracionAuth;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo configuraci�n din�mica para API {IdAPI}", idApi);
            return new Dictionary<string, object>();
        }
    }

    // =====================================================
    // M�TODOS AUXILIARES
    // =====================================================

    private string GenerateExampleCall(int idApi, List<ParameterInfo> parametros)
    {
        var baseUrl = "/api/execute";
        var queryParams = new List<string> { $"idApi={idApi}" };

        foreach (var param in parametros.Where(p => p.Requerido))
        {
            var exampleValue = GetExampleValue(param.Tipo);
            queryParams.Add($"{param.Nombre}={exampleValue}");
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

    public async Task<bool> TestApiConnectionAsync(int idApi, string environment = "TEST")
    {
        try
        {
            var config = await GetApiConfigurationAsync(idApi);
            if (config == null) return false;

            var connectionString = environment.ToUpper() == "TEST"
                ? config.StringConexionTest
                : config.StringConexionProduccion;

            if (string.IsNullOrEmpty(connectionString)) return false;

            // Probar conexi�n simple
            using var connection = await _dbService.GetConnectionAsync(connectionString);
            await connection.QueryAsync("SELECT 1");

            _logger.LogInformation("Prueba de conexi�n exitosa para API {IdAPI} en ambiente {Environment}", idApi, environment);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en prueba de conexi�n para API {IdAPI} en ambiente {Environment}", idApi, environment);
            return false;
        }
    }

    // =====================================================
    // MODELOS AUXILIARES
    // =====================================================

    public class ApiAuthMetadata
    {
        public int IdAPI { get; set; }
        public string NombreAPI { get; set; } = string.Empty;
        public string TipoAutenticacion { get; set; } = string.Empty;
        public string NombreTipoAuth { get; set; } = string.Empty;
        public bool RequiereConfiguracion { get; set; }
        public string? ConfiguracionAuth { get; set; }
        public int RateLimitPorMinuto { get; set; }
        public int TotalCredenciales { get; set; }
        public int CredencialesActivas { get; set; }
    }
}