// Servicio de auditoría
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

public class AuditService : IAuditService
{
    private readonly DatabaseOptions _dbOptions;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IOptions<DatabaseOptions> dbOptions, ILogger<AuditService> logger)
    {
        _dbOptions = dbOptions.Value;
        _logger = logger;
    }

    public async Task LogExecutionAsync(AuditLog auditLog)
    {
        try
        {
            using var connection = new SqlConnection(_dbOptions.ConfigConnectionString);

            var parameters = new DynamicParameters();
            parameters.Add("@IdAPI", auditLog.IdAPI);
            parameters.Add("@IdToken", auditLog.IdToken);
            parameters.Add("@Ambiente", auditLog.Ambiente);
            parameters.Add("@ParametrosEnviados", auditLog.ParametrosEnviados);
            parameters.Add("@EsExitoso", auditLog.EsExitoso);
            parameters.Add("@MensajeError", auditLog.MensajeError);
            parameters.Add("@TiempoEjecucionMs", auditLog.TiempoEjecucionMs);
            parameters.Add("@DireccionIP", auditLog.DireccionIP);

            await connection.ExecuteAsync("sp_LogAuditoria", parameters,
                commandType: CommandType.StoredProcedure, commandTimeout: _dbOptions.DefaultCommandTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando log de auditoría para API {IdAPI}", auditLog.IdAPI);
        }
    }

    public async Task<List<UsageStatistics>> GetUsageStatisticsAsync(int? idApi = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        try
        {
            using var connection = new SqlConnection(_dbOptions.ConfigConnectionString);

            var parameters = new DynamicParameters();
            parameters.Add("@IdAPI", idApi);
            parameters.Add("@FechaDesde", fechaDesde);
            parameters.Add("@FechaHasta", fechaHasta);

            var result = await connection.QueryAsync<UsageStatistics>("sp_GetUsageStatistics", parameters,
                commandType: CommandType.StoredProcedure, commandTimeout: _dbOptions.DefaultCommandTimeout);

            return result.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas de uso");
            throw;
        }
    }
}