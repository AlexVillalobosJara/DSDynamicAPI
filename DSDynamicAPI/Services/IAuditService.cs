// =====================================================
// IAuditService - ACTUALIZADA para credenciales
// =====================================================
public interface IAuditService
{
    // Métodos existentes actualizados (IdToken ? IdCredencial)
    Task<List<AuditLog>> GetAuditLogsAsync(int? idAPI = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null, int pageSize = 50, int pageNumber = 1);
    Task<List<AuditLog>> GetRecentErrorsAsync(int count = 10);
    Task<List<UsageStatistics>> GetUsageStatisticsAsync(int? idAPI = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null);

    // MÉTODO ACTUALIZADO: IdToken ? IdCredencial
    Task LogAuditoriaAsync(int idAPI, int? idCredencial, string ambiente, string? parametrosEnviados, bool esExitoso, string? mensajeError, int tiempoEjecucionMs, string? direccionIP);

    Task<long> GetTotalExecutionsAsync(DateTime? fechaDesde = null);
    Task<double> GetAverageExecutionTimeAsync(int? idAPI = null, DateTime? fechaDesde = null);
    Task<double> GetSuccessRateAsync(int? idAPI = null, DateTime? fechaDesde = null);

    // NUEVOS MÉTODOS para análisis por tipo de autenticación
    Task<List<AuditLog>> GetAuditLogsByAuthTypeAsync(TipoAutenticacion tipoAuth, DateTime? fechaDesde = null, DateTime? fechaHasta = null);
    Task<Dictionary<string, int>> GetExecutionsByAuthTypeAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null);
    Task<List<AuditLog>> GetAuditLogsByCredentialAsync(int idCredencial, DateTime? fechaDesde = null, DateTime? fechaHasta = null);
    Task<double> GetSuccessRateByAuthTypeAsync(TipoAutenticacion tipoAuth, DateTime? fechaDesde = null);
}