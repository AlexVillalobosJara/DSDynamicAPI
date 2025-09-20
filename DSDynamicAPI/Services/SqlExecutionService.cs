// =====================================================
// SqlExecutionService - IMPLEMENTACIÓN ACTUALIZADA
// =====================================================

using Dapper;
using DSDynamicAPI.Services;
using DynamicAPIs.Services.Database;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DynamicAPIs.Services.Implementation;

public class SqlExecutionService : ISqlExecutionService
{
    private readonly DatabaseService _dbService;
    private readonly ILogger<SqlExecutionService> _logger;

    public SqlExecutionService(DatabaseService dbService, ILogger<SqlExecutionService> logger)
    {
        _dbService = dbService;
        _logger = logger;
    }

    // =====================================================
    // MÉTODO PRINCIPAL DE EJECUCIÓN
    // =====================================================

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
            _logger.LogInformation("Ejecutando {TipoObjeto} {ObjetoSQL} en ambiente {Environment} para API {IdAPI}",
                config.TipoObjeto, config.ObjetoSQL, environment, config.IdAPI);

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
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Error SQL ejecutando {TipoObjeto} {ObjetoSQL} para API {IdAPI}: {ErrorMessage}",
                config.TipoObjeto, config.ObjetoSQL, config.IdAPI, ex.Message);

            throw new InvalidOperationException($"Error de base de datos: {GetFriendlyErrorMessage(ex)}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado ejecutando {TipoObjeto} {ObjetoSQL} para API {IdAPI}",
                config.TipoObjeto, config.ObjetoSQL, config.IdAPI);
            throw;
        }
    }

    // =====================================================
    // NUEVO MÉTODO CON CONTEXTO DE AUTENTICACIÓN
    // =====================================================

    public async Task<object?> ExecuteSqlWithAuthContextAsync(ApiConfiguration config, Dictionary<string, object?> parameters, string environment, AuthValidationResult authContext)
    {
        // Agregar metadatos de autenticación a los parámetros si es necesario
        var enrichedParameters = new Dictionary<string, object?>(parameters);

        // Agregar información de contexto de autenticación
        if (authContext.IdCredencial.HasValue)
        {
            enrichedParameters["@_AuthCredentialId"] = authContext.IdCredencial.Value;
        }

        enrichedParameters["@_AuthType"] = authContext.TipoAuth;
        enrichedParameters["@_ExecutionContext"] = environment;

        // Agregar metadatos adicionales si están disponibles
        if (authContext.AuthMetadata?.Any() == true)
        {
            foreach (var metadata in authContext.AuthMetadata)
            {
                var key = $"@_Auth{metadata.Key}";
                if (!enrichedParameters.ContainsKey(key))
                {
                    enrichedParameters[key] = metadata.Value;
                }
            }
        }

        _logger.LogDebug("Ejecutando SQL con contexto de autenticación: {TipoAuth}, Credencial: {IdCredencial}",
            authContext.TipoAuth, authContext.IdCredencial);

        return await ExecuteSqlAsync(config, enrichedParameters, environment);
    }

    // =====================================================
    // EJECUCIÓN DE STORED PROCEDURES
    // =====================================================

    private async Task<object?> ExecuteStoredProcedureAsync(SqlConnection connection, ApiConfiguration config, DynamicParameters parameters)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var result = await connection.QueryAsync(config.ObjetoSQL, parameters,
                commandType: CommandType.StoredProcedure,
                commandTimeout: config.TimeoutEjecucionSegundos);

            stopwatch.Stop();

            _logger.LogInformation("Stored procedure {ProcedureName} ejecutado exitosamente en {ElapsedMs}ms, {RowCount} filas retornadas",
                config.ObjetoSQL, stopwatch.ElapsedMilliseconds, result.Count());

            return ProcessResult(result);
        }
        catch (SqlException ex) when (ex.Number == 2) // Timeout
        {
            _logger.LogWarning("Timeout ejecutando stored procedure {ProcedureName} después de {Timeout}s",
                config.ObjetoSQL, config.TimeoutEjecucionSegundos);

            throw new TimeoutException($"La ejecución del procedimiento {config.ObjetoSQL} excedió el tiempo límite de {config.TimeoutEjecucionSegundos} segundos");
        }
        catch (SqlException ex) when (ex.Number == 2812) // Procedure not found
        {
            _logger.LogError("Stored procedure {ProcedureName} no encontrado", config.ObjetoSQL);
            throw new InvalidOperationException($"El procedimiento almacenado '{config.ObjetoSQL}' no existe");
        }
        catch (SqlException ex) when (ex.Number == 201 || ex.Number == 8144) // Parameter errors
        {
            _logger.LogError("Error de parámetros en stored procedure {ProcedureName}: {ErrorMessage}",
                config.ObjetoSQL, ex.Message);

            throw new ArgumentException($"Error en los parámetros del procedimiento: {ex.Message}");
        }
    }

    // =====================================================
    // EJECUCIÓN DE FUNCIONES
    // =====================================================

    private async Task<object?> ExecuteFunctionAsync(SqlConnection connection, ApiConfiguration config, DynamicParameters parameters)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Para funciones escalares
            if (await IsFunctionScalar(connection, config.ObjetoSQL))
            {
                var functionCall = BuildScalarFunctionCall(config.ObjetoSQL, parameters);
                var result = await connection.QueryFirstOrDefaultAsync<object>(functionCall, parameters,
                    commandTimeout: config.TimeoutEjecucionSegundos);

                stopwatch.Stop();
                _logger.LogInformation("Función escalar {FunctionName} ejecutada en {ElapsedMs}ms",
                    config.ObjetoSQL, stopwatch.ElapsedMilliseconds);

                return result;
            }
            // Para funciones de tabla
            else
            {
                var functionCall = BuildTableFunctionCall(config.ObjetoSQL, parameters);
                var result = await connection.QueryAsync(functionCall, parameters,
                    commandTimeout: config.TimeoutEjecucionSegundos);

                stopwatch.Stop();
                _logger.LogInformation("Función de tabla {FunctionName} ejecutada en {ElapsedMs}ms, {RowCount} filas retornadas",
                    config.ObjetoSQL, stopwatch.ElapsedMilliseconds, result.Count());

                return ProcessResult(result);
            }
        }
        catch (SqlException ex) when (ex.Number == 2812) // Function not found
        {
            _logger.LogError("Función {FunctionName} no encontrada", config.ObjetoSQL);
            throw new InvalidOperationException($"La función '{config.ObjetoSQL}' no existe");
        }
    }

    // =====================================================
    // EJECUCIÓN DE VISTAS
    // =====================================================

    private async Task<object?> ExecuteViewAsync(SqlConnection connection, ApiConfiguration config, DynamicParameters parameters)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var query = BuildViewQuery(config.ObjetoSQL, parameters);
            var result = await connection.QueryAsync(query, parameters,
                commandTimeout: config.TimeoutEjecucionSegundos);

            stopwatch.Stop();

            _logger.LogInformation("Vista {ViewName} consultada exitosamente en {ElapsedMs}ms, {RowCount} filas retornadas",
                config.ObjetoSQL, stopwatch.ElapsedMilliseconds, result.Count());

            return ProcessResult(result);
        }
        catch (SqlException ex) when (ex.Number == 208) // Object not found
        {
            _logger.LogError("Vista {ViewName} no encontrada", config.ObjetoSQL);
            throw new InvalidOperationException($"La vista '{config.ObjetoSQL}' no existe");
        }
    }

    // =====================================================
    // VALIDACIÓN Y CONVERSIÓN DE PARÁMETROS
    // =====================================================

    private DynamicParameters ValidateAndConvertParameters(List<ApiParameter> configParams, Dictionary<string, object?> providedParams)
    {
        var dynamicParams = new DynamicParameters();

        foreach (var configParam in configParams)
        {
            var paramName = configParam.NombreParametro.StartsWith("@")
                ? configParam.NombreParametro
                : "@" + configParam.NombreParametro;

            object? value = null;

            // Buscar el valor en los parámetros proporcionados (con o sin @)
            if (providedParams.TryGetValue(configParam.NombreParametro, out value) ||
                providedParams.TryGetValue(paramName, out value))
            {
                // Convertir el valor al tipo correcto
                value = ConvertParameterValue(value, configParam.TipoParametro);
            }
            else if (!string.IsNullOrEmpty(configParam.ValorPorDefecto))
            {
                // Usar valor por defecto
                value = ConvertParameterValue(configParam.ValorPorDefecto, configParam.TipoParametro);
            }
            else if (configParam.EsObligatorio)
            {
                throw new ArgumentException($"Parámetro obligatorio '{configParam.NombreParametro}' no proporcionado");
            }

            // Agregar el parámetro con el tipo SQL correcto
            var dbType = GetDbType(configParam.TipoParametro);
            dynamicParams.Add(paramName, value, dbType);

            _logger.LogDebug("Parámetro agregado: {ParamName} = {Value} (Tipo: {DbType})",
                paramName, value ?? "NULL", dbType);
        }

        return dynamicParams;
    }

    private object? ConvertParameterValue(object? value, string targetType)
    {
        if (value == null || value == DBNull.Value)
            return null;

        var stringValue = value.ToString();
        if (string.IsNullOrEmpty(stringValue))
            return null;

        try
        {
            return targetType.ToUpper() switch
            {
                "INT" or "INTEGER" => int.Parse(stringValue),
                "BIGINT" => long.Parse(stringValue),
                "DECIMAL" or "NUMERIC" => decimal.Parse(stringValue),
                "FLOAT" or "REAL" => float.Parse(stringValue),
                "DOUBLE" => double.Parse(stringValue),
                "DATETIME" or "DATETIME2" or "SMALLDATETIME" => DateTime.Parse(stringValue),
                "DATE" => DateOnly.Parse(stringValue),
                "TIME" => TimeOnly.Parse(stringValue),
                "BIT" or "BOOLEAN" => bool.Parse(stringValue),
                "UNIQUEIDENTIFIER" => Guid.Parse(stringValue),
                _ => stringValue // Default para strings y tipos no reconocidos
            };
        }
        catch (FormatException)
        {
            throw new ArgumentException($"El valor '{stringValue}' no puede ser convertido al tipo {targetType}");
        }
    }

    private DbType GetDbType(string sqlType)
    {
        return sqlType.ToUpper() switch
        {
            "INT" or "INTEGER" => DbType.Int32,
            "BIGINT" => DbType.Int64,
            "SMALLINT" => DbType.Int16,
            "TINYINT" => DbType.Byte,
            "DECIMAL" or "NUMERIC" => DbType.Decimal,
            "FLOAT" => DbType.Double,
            "REAL" => DbType.Single,
            "MONEY" or "SMALLMONEY" => DbType.Currency,
            "DATETIME" or "DATETIME2" => DbType.DateTime,
            "SMALLDATETIME" => DbType.DateTime,
            "DATE" => DbType.Date,
            "TIME" => DbType.Time,
            "BIT" => DbType.Boolean,
            "UNIQUEIDENTIFIER" => DbType.Guid,
            "NVARCHAR" or "NCHAR" or "NTEXT" => DbType.String,
            "VARCHAR" or "CHAR" or "TEXT" => DbType.AnsiString,
            "BINARY" or "VARBINARY" or "IMAGE" => DbType.Binary,
            _ => DbType.String
        };
    }

    // =====================================================
    // MÉTODOS AUXILIARES
    // =====================================================

    private async Task<bool> IsFunctionScalar(SqlConnection connection, string functionName)
    {
        const string sql = @"
            SELECT DATA_TYPE
            FROM INFORMATION_SCHEMA.ROUTINES
            WHERE ROUTINE_NAME = @FunctionName AND ROUTINE_TYPE = 'FUNCTION'";

        var dataType = await connection.QueryFirstOrDefaultAsync<string>(sql, new { FunctionName = functionName });

        // Si retorna TABLE, es función de tabla; cualquier otro tipo es escalar
        return !string.Equals(dataType, "TABLE", StringComparison.OrdinalIgnoreCase);
    }

    private string BuildScalarFunctionCall(string functionName, DynamicParameters parameters)
    {
        var paramNames = string.Join(", ", parameters.ParameterNames.Select(p => p));
        return $"SELECT dbo.{functionName}({paramNames}) AS Result";
    }

    private string BuildTableFunctionCall(string functionName, DynamicParameters parameters)
    {
        var paramNames = string.Join(", ", parameters.ParameterNames.Select(p => p));
        return $"SELECT * FROM dbo.{functionName}({paramNames})";
    }

    private string BuildViewQuery(string viewName, DynamicParameters parameters)
    {
        var query = $"SELECT * FROM {viewName}";

        // Si hay parámetros, construir WHERE clause
        if (parameters.ParameterNames.Any())
        {
            var whereConditions = parameters.ParameterNames.Select(param =>
            {
                var columnName = param.StartsWith("@") ? param[1..] : param;
                return $"{columnName} = {param}";
            });

            query += " WHERE " + string.Join(" AND ", whereConditions);
        }

        return query;
    }

    private object ProcessResult(IEnumerable<dynamic> result)
    {
        var resultList = result.ToList();

        if (!resultList.Any())
        {
            return new { Message = "No se encontraron datos", Count = 0 };
        }

        // Si es un solo resultado, retornar el objeto directamente
        if (resultList.Count == 1)
        {
            return resultList.First();
        }

        // Si son múltiples resultados, retornar como array
        return resultList;
    }

    private string GetFriendlyErrorMessage(SqlException ex)
    {
        return ex.Number switch
        {
            2 => "Tiempo de espera agotado. La consulta tardó demasiado en ejecutarse.",
            18 => "Error de sintaxis en la consulta SQL.",
            102 => "Sintaxis incorrecta cerca de un token.",
            208 => "El objeto especificado no existe en la base de datos.",
            229 => "Permisos insuficientes para ejecutar la operación.",
            515 => "No se puede insertar NULL en una columna que no permite valores nulos.",
            547 => "Violación de restricción de clave foránea.",
            2812 => "El procedimiento almacenado especificado no existe.",
            8144 => "El procedimiento tiene demasiados argumentos especificados.",
            _ => $"Error SQL #{ex.Number}: {ex.Message}"
        };
    }

    // =====================================================
    // MÉTODOS DE DIAGNÓSTICO Y MONITOREO
    // =====================================================

    public async Task<bool> TestConnectionAsync(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await connection.QueryAsync("SELECT 1");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error probando conexión: {ConnectionString}",
                MaskConnectionString(connectionString));
            return false;
        }
    }

    public async Task<object> GetDatabaseInfoAsync(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT 
                    @@SERVERNAME as ServerName,
                    DB_NAME() as DatabaseName,
                    @@VERSION as Version,
                    GETDATE() as CurrentTime,
                    @@SPID as ProcessId";

            var info = await connection.QueryFirstOrDefaultAsync(sql);
            return info ?? new { Error = "No se pudo obtener información de la base de datos" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo información de base de datos");
            return new { Error = ex.Message };
        }
    }

    private string MaskConnectionString(string connectionString)
    {
        // Enmascarar passwords en connection strings para logging seguro
        return System.Text.RegularExpressions.Regex.Replace(
            connectionString,
            @"(Password|Pwd)\s*=\s*[^;]+",
            "$1=***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}