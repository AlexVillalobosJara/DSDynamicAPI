// Servicio de configuración
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

public class ConfigurationService : IConfigurationService
{
    private readonly DatabaseOptions _dbOptions;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(IOptions<DatabaseOptions> dbOptions, ILogger<ConfigurationService> logger)
    {
        _dbOptions = dbOptions.Value;
        _logger = logger;
    }

    public async Task<ApiConfiguration?> GetApiConfigurationAsync(int idApi, string? token = null)
    {
        try
        {
            using var connection = new SqlConnection(_dbOptions.ConfigConnectionString);

            var parameters = new DynamicParameters();
            parameters.Add("@IdAPI", idApi);
            parameters.Add("@TokenValue", token);

            using var multi = await connection.QueryMultipleAsync("sp_GetAPIConfig", parameters,
                commandType: CommandType.StoredProcedure, commandTimeout: _dbOptions.DefaultCommandTimeout);

            var apiConfig = await multi.ReadFirstOrDefaultAsync<ApiConfiguration>();
            if (apiConfig == null) return null;

            var parametros = await multi.ReadAsync<ApiParameter>();
            apiConfig.Parametros = parametros.ToList();

            var IdToken = await multi.ReadAsync<int>();
            if (IdToken != null && IdToken.Count() >= 0)
            {
                apiConfig.IdToken = IdToken.FirstOrDefault();
            }

            return apiConfig;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo configuración de API {IdAPI}", idApi);
            throw;
        }
    }

    public async Task<List<ApiInfo>> GetAvailableApisAsync()
    {
        try
        {
            using var connection = new SqlConnection(_dbOptions.ConfigConnectionString);

            var sql = @"
                    SELECT 
                        a.IdAPI,
                        a.NombreAPI,
                        a.Descripcion,
                        a.TipoObjeto
                    FROM APIs a
                    WHERE a.EsActivo = 1
                    ORDER BY a.NombreAPI";

            var apis = await connection.QueryAsync<ApiInfo>(sql);

            foreach (var api in apis)
            {
                var paramSql = @"
                        SELECT 
                            NombreParametro as Nombre,
                            TipoParametro as Tipo,
                            EsObligatorio as Requerido,
                            ValorPorDefecto,
                            Descripcion
                        FROM Parametros
                        WHERE IdAPI = @IdAPI
                        ORDER BY Orden";

                var parametros = await connection.QueryAsync<ParameterInfo>(paramSql, new { IdAPI = api.IdAPI });
                api.Parametros = parametros.ToList();
                api.Endpoint = $"/api/execute?IdAPI={api.IdAPI}";
                api.ExampleCall = GenerateExampleCall(api);
            }

            return apis.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo APIs disponibles");
            throw;
        }
    }

    private string GenerateExampleCall(ApiInfo api)
    {
        var requiredParams = api.Parametros.Where(p => p.Requerido).ToList();
        if (!requiredParams.Any()) return api.Endpoint;

        var paramString = string.Join("&", requiredParams.Select(p =>
            $"{p.Nombre}={GetExampleValue(p.Tipo)}"));

        return $"{api.Endpoint}&{paramString}";
    }

    private string GetExampleValue(string tipo)
    {
        return tipo.ToUpper() switch
        {
            "INT" => "123",
            "DECIMAL" => "123.45",
            "DATETIME" => DateTime.Now.ToString("yyyy-MM-dd"),
            "BIT" => "true",
            _ => "example"
        };
    }
}