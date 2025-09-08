// Servicio de ejecución SQL
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

public class SqlExecutionService : ISqlExecutionService
{
    private readonly ILogger<SqlExecutionService> _logger;

    public SqlExecutionService(ILogger<SqlExecutionService> logger)
    {
        _logger = logger;
    }

    public async Task<object?> ExecuteSqlAsync(ApiConfiguration config, Dictionary<string, object?> parameters, string environment)
    {
        var connectionString = environment.ToUpper() == "TEST"
            ? config.StringConexionTest
            : config.StringConexionProduccion;

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"String de conexión no configurado para ambiente {environment}");
        }

        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            var validatedParams = ValidateAndConvertParameters(config.Parametros, parameters);

            return config.TipoObjeto.ToUpper() switch
            {
                "PROCEDURE" => await ExecuteStoredProcedureAsync(connection, config, validatedParams),
                "FUNCTION" => await ExecuteFunctionAsync(connection, config, validatedParams),
                "VIEW" => await ExecuteViewAsync(connection, config, validatedParams),
                _ => throw new NotSupportedException($"Tipo de objeto SQL no soportado: {config.TipoObjeto}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando SQL para API {IdAPI}", config.IdAPI);
            throw;
        }
    }

    private async Task<object?> ExecuteStoredProcedureAsync(SqlConnection connection, ApiConfiguration config, DynamicParameters parameters)
    {
        try
        {
            var result = await connection.QueryAsync(config.ObjetoSQL, parameters,
                commandType: CommandType.StoredProcedure, commandTimeout: config.TimeoutEjecucionSegundos);

            return result.ToList();
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error ejecutando stored procedure {ProcedureName}", config.ObjetoSQL);
            throw new InvalidOperationException($"Error ejecutando procedimiento: {ex.Message}", ex);
        }
    }

    private async Task<object?> ExecuteFunctionAsync(SqlConnection connection, ApiConfiguration config, DynamicParameters parameters)
    {
        try
        {
            var paramList = string.Join(", ", parameters.ParameterNames.Select(p => $"@{p}"));
            var sql = $"SELECT * FROM {config.ObjetoSQL}({paramList})";

            var result = await connection.QueryAsync(sql, parameters, commandTimeout: config.TimeoutEjecucionSegundos);
            return result.ToList();
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error ejecutando función {FunctionName}", config.ObjetoSQL);
            throw new InvalidOperationException($"Error ejecutando función: {ex.Message}", ex);
        }
    }

    private async Task<object?> ExecuteViewAsync(SqlConnection connection, ApiConfiguration config, DynamicParameters parameters)
    {
        try
        {
            var whereClause = BuildWhereClause(parameters);
            var sql = $"SELECT * FROM {config.ObjetoSQL}{whereClause}";

            var result = await connection.QueryAsync(sql, parameters, commandTimeout: config.TimeoutEjecucionSegundos);
            return result.ToList();
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error ejecutando vista {ViewName}", config.ObjetoSQL);
            throw new InvalidOperationException($"Error ejecutando vista: {ex.Message}", ex);
        }
    }

    private DynamicParameters ValidateAndConvertParameters(List<ApiParameter> configParams, Dictionary<string, object?> inputParams)
    {
        var dynamicParams = new DynamicParameters();

        foreach (var configParam in configParams)
        {
            var hasValue = inputParams.TryGetValue(configParam.NombreParametro, out var value);

            if (!hasValue || value == null)
            {
                if (configParam.EsObligatorio && string.IsNullOrEmpty(configParam.ValorPorDefecto))
                {
                    throw new ArgumentException($"Parámetro obligatorio faltante: {configParam.NombreParametro}");
                }

                value = ConvertDefaultValue(configParam.ValorPorDefecto, configParam.TipoParametro);
            }
            else
            {
                value = ConvertParameterValue(value, configParam.TipoParametro);
            }

            if (value != null)
            {
                dynamicParams.Add(configParam.NombreParametro, value);
            }
        }

        return dynamicParams;
    }

    private object? ConvertParameterValue(object? value, string tipo)
    {
        if (value == null) return null;

        try
        {
            return tipo.ToUpper() switch
            {
                "INT" => Convert.ToInt32(value),
                "DECIMAL" => Convert.ToDecimal(value),
                "DATETIME" => Convert.ToDateTime(value),
                "BIT" => Convert.ToBoolean(value),
                "NVARCHAR" => value.ToString(),
                _ => value.ToString()
            };
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Error convirtiendo parámetro a tipo {tipo}: {ex.Message}");
        }
    }

    private object? ConvertDefaultValue(string? defaultValue, string tipo)
    {
        if (string.IsNullOrEmpty(defaultValue)) return null;
        return ConvertParameterValue(defaultValue, tipo);
    }

    private string BuildWhereClause(DynamicParameters parameters)
    {
        if (!parameters.ParameterNames.Any()) return "";

        var conditions = parameters.ParameterNames.Select(p => $"{p} = @{p}");
        return $" WHERE {string.Join(" AND ", conditions)}";
    }

    private string ExtractDatabaseName(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.InitialCatalog ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}