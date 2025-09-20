// =====================================================
// DatabaseService - SERVICIO BASE DE DATOS COMPLETO
// =====================================================

using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace DynamicAPIs.Services.Database;

public class DatabaseService
{
    private readonly DatabaseOptions _options;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(IOptions<DatabaseOptions> options, ILogger<DatabaseService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    // =====================================================
    // MÉTODOS DE CONEXIÓN
    // =====================================================

    public async Task<IDbConnection> GetConnectionAsync(string? connectionString = null)
    {
        var connString = connectionString ?? _options.ConfigConnectionString;

        if (string.IsNullOrEmpty(connString))
        {
            throw new InvalidOperationException("String de conexión no configurado");
        }

        var connection = new SqlConnection(connString);
        await connection.OpenAsync();

        return connection;
    }

    public IDbConnection GetConnection(string? connectionString = null)
    {
        var connString = connectionString ?? _options.ConfigConnectionString;

        if (string.IsNullOrEmpty(connString))
        {
            throw new InvalidOperationException("String de conexión no configurado");
        }

        var connection = new SqlConnection(connString);
        connection.Open();

        return connection;
    }

    // =====================================================
    // MÉTODOS DE CONSULTA ASÍNCRONOS
    // =====================================================

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QueryAsync<T>(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("Query ejecutado exitosamente: {RowCount} filas retornadas", result.Count());

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando query: {Sql}", sql);
            throw;
        }
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QueryFirstOrDefaultAsync<T>(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("QueryFirstOrDefault ejecutado exitosamente");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando QueryFirstOrDefault: {Sql}", sql);
            throw;
        }
    }

    public async Task<T> QuerySingleAsync<T>(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QuerySingleAsync<T>(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("QuerySingle ejecutado exitosamente");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando QuerySingle: {Sql}", sql);
            throw;
        }
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.ExecuteAsync(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("Execute ejecutado exitosamente: {RowsAffected} filas afectadas", result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando Execute: {Sql}", sql);
            throw;
        }
    }

    public async Task<T> ExecuteScalarAsync<T>(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.ExecuteScalarAsync<T>(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("ExecuteScalar ejecutado exitosamente");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando ExecuteScalar: {Sql}", sql);
            throw;
        }
    }

    // =====================================================
    // MÉTODOS ESPECÍFICOS PARA TIPOS SEGUROS
    // =====================================================

    public async Task<long> QueryFirstOrDefaultAsLongAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QueryFirstOrDefaultAsync<long?>(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("QueryFirstOrDefaultAsLong ejecutado exitosamente");

            return result ?? 0L;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando QueryFirstOrDefaultAsLong: {Sql}", sql);
            throw;
        }
    }

    public async Task<double> QueryFirstOrDefaultAsDoubleAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QueryFirstOrDefaultAsync<double?>(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("QueryFirstOrDefaultAsDouble ejecutado exitosamente");

            return result ?? 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando QueryFirstOrDefaultAsDouble: {Sql}", sql);
            throw;
        }
    }

    public async Task<int> QueryFirstOrDefaultAsIntAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QueryFirstOrDefaultAsync<int?>(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("QueryFirstOrDefaultAsInt ejecutado exitosamente");

            return result ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando QueryFirstOrDefaultAsInt: {Sql}", sql);
            throw;
        }
    }

    public async Task<string> QueryFirstOrDefaultAsStringAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QueryFirstOrDefaultAsync<string?>(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("QueryFirstOrDefaultAsString ejecutado exitosamente");

            return result ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando QueryFirstOrDefaultAsString: {Sql}", sql);
            throw;
        }
    }

    public async Task<bool> QueryFirstOrDefaultAsBoolAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QueryFirstOrDefaultAsync<bool?>(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("QueryFirstOrDefaultAsBool ejecutado exitosamente");

            return result ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando QueryFirstOrDefaultAsBool: {Sql}", sql);
            throw;
        }
    }

    // =====================================================
    // MÉTODOS DE STORED PROCEDURES
    // =====================================================

    public async Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(string procedureName, object? parameters = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QueryAsync<T>(
                procedureName,
                parameters,
                commandType: CommandType.StoredProcedure,
                commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("Stored procedure {ProcedureName} ejecutado exitosamente: {RowCount} filas retornadas",
                procedureName, result.Count());

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando stored procedure: {ProcedureName}", procedureName);
            throw;
        }
    }

    public async Task<T?> ExecuteStoredProcedureFirstOrDefaultAsync<T>(string procedureName, object? parameters = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.QueryFirstOrDefaultAsync<T>(
                procedureName,
                parameters,
                commandType: CommandType.StoredProcedure,
                commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("Stored procedure {ProcedureName} ejecutado exitosamente", procedureName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando stored procedure: {ProcedureName}", procedureName);
            throw;
        }
    }

    public async Task<int> ExecuteStoredProcedureAsync(string procedureName, object? parameters = null, int? commandTimeout = null)
    {
        try
        {
            using var connection = await GetConnectionAsync();

            var result = await connection.ExecuteAsync(
                procedureName,
                parameters,
                commandType: CommandType.StoredProcedure,
                commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("Stored procedure {ProcedureName} ejecutado exitosamente: {RowsAffected} filas afectadas",
                procedureName, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando stored procedure: {ProcedureName}", procedureName);
            throw;
        }
    }

    // =====================================================
    // MÉTODOS DE TRANSACCIONES
    // =====================================================

    public async Task<T> ExecuteInTransactionAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> operation)
    {
        using var connection = await GetConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            var result = await operation(connection, transaction);
            transaction.Commit();

            _logger.LogDebug("Transacción ejecutada exitosamente");

            return result;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error en transacción, se realizó rollback");
            throw;
        }
    }

    public async Task ExecuteInTransactionAsync(Func<IDbConnection, IDbTransaction, Task> operation)
    {
        using var connection = await GetConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            await operation(connection, transaction);
            transaction.Commit();

            _logger.LogDebug("Transacción ejecutada exitosamente");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error en transacción, se realizó rollback");
            throw;
        }
    }

    // =====================================================
    // MÉTODOS DE MÚLTIPLES RESULTADOS
    // =====================================================

    public async Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        try
        {
            var connection = await GetConnectionAsync();

            var result = await connection.QueryMultipleAsync(sql, param, commandTimeout: commandTimeout ?? _options.DefaultCommandTimeout);

            _logger.LogDebug("QueryMultiple ejecutado exitosamente");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando QueryMultiple: {Sql}", sql);
            throw;
        }
    }

    // =====================================================
    // MÉTODOS DE UTILIDAD
    // =====================================================

    public async Task<bool> TestConnectionAsync(string? connectionString = null)
    {
        try
        {
            using var connection = await GetConnectionAsync(connectionString);
            await connection.QueryAsync("SELECT 1");

            _logger.LogInformation("Prueba de conexión exitosa");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en prueba de conexión");
            return false;
        }
    }

    public async Task<string> GetDatabaseVersionAsync()
    {
        try
        {
            using var connection = await GetConnectionAsync();
            var version = await connection.QueryFirstOrDefaultAsync<string>("SELECT @@VERSION");

            return version ?? "Versión desconocida";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo versión de base de datos");
            return "Error obteniendo versión";
        }
    }

    public async Task<bool> TableExistsAsync(string tableName, string? schema = "dbo")
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";

            using var connection = await GetConnectionAsync();
            var count = await connection.QueryFirstOrDefaultAsync<int>(sql, new { Schema = schema, TableName = tableName });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando existencia de tabla {TableName}", tableName);
            return false;
        }
    }

    public async Task<bool> StoredProcedureExistsAsync(string procedureName, string? schema = "dbo")
    {
        try
        {
            const string sql = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.ROUTINES 
                WHERE ROUTINE_SCHEMA = @Schema AND ROUTINE_NAME = @ProcedureName AND ROUTINE_TYPE = 'PROCEDURE'";

            using var connection = await GetConnectionAsync();
            var count = await connection.QueryFirstOrDefaultAsync<int>(sql, new { Schema = schema, ProcedureName = procedureName });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando existencia de procedimiento {ProcedureName}", procedureName);
            return false;
        }
    }

    // =====================================================
    // MÉTODOS AUXILIARES ADICIONALES
    // =====================================================

    public async Task<List<T>> QueryToListAsync<T>(string sql, object? param = null, int? commandTimeout = null)
    {
        var result = await QueryAsync<T>(sql, param, commandTimeout);
        return result.ToList();
    }

    public async Task<bool> ExistsAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        var count = await QueryFirstOrDefaultAsIntAsync($"SELECT COUNT(*) FROM ({sql}) AS CountQuery", param, commandTimeout);
        return count > 0;
    }

    public string GetConnectionString() => _options.ConfigConnectionString;

    public int GetDefaultTimeout() => _options.DefaultCommandTimeout;

    public void Dispose()
    {
        _logger.LogDebug("DatabaseService disposed");
    }

    /// <summary>
    /// Ejecuta una consulta y retorna el primer resultado como int, 0 si es null
    /// </summary>
    public async Task<int> QuerySingleIntAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        var result = await QueryFirstOrDefaultAsync<int?>(sql, param, commandTimeout);
        return result ?? 0;
    }

    /// <summary>
    /// Ejecuta una consulta y retorna el primer resultado como long, 0 si es null
    /// </summary>
    public async Task<long> QuerySingleLongAsync(string sql, object? param = null, int? commandTimeout = null)
    {
        var result = await QueryFirstOrDefaultAsync<long?>(sql, param, commandTimeout);
        return result ?? 0L;
    }

    public bool IsConfigured() => !string.IsNullOrEmpty(_options.ConfigConnectionString);
}

// =====================================================
// OPCIONES DE CONFIGURACIÓN
// =====================================================
public class DatabaseOptions
{
    public string ConfigConnectionString { get; set; } = string.Empty;
    public int DefaultCommandTimeout { get; set; } = 30;
    public bool EnableRetry { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
    public bool LogQueries { get; set; } = false;
    public bool EnablePerformanceCounters { get; set; } = false;
}