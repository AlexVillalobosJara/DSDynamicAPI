// Interfaz para auditoría
public interface IAuditService
{
    Task LogExecutionAsync(AuditLog auditLog);
    Task<List<UsageStatistics>> GetUsageStatisticsAsync(int? idApi = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null);
}