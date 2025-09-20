// =====================================================
// AuditService - IMPLEMENTACIÓN CORREGIDA PARA DATABASESERVICE
// =====================================================

using DynamicAPIs.Services.Database;
using Dapper;

namespace DynamicAPIs.Services.Implementation;

public class AuditService : IAuditService
{
    private readonly DatabaseService _dbService;
    private readonly ILogger<AuditService> _logger;

    public AuditService(DatabaseService dbService, ILogger<AuditService> logger)
    {
        _dbService = dbService;
        _logger = logger;
    }

    // =====================================================
    // MÉTODOS PRINCIPALES ACTUALIZADOS (IdToken → IdCredencial)
    // =====================================================

    public async Task<List<AuditLog>> GetAuditLogsAsync(int? idAPI = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null, int pageSize = 50, int pageNumber = 1)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);
        fechaHasta ??= DateTime.Now;

        const string sql = @"
            SELECT 
                al.IdAPI, al.IdCredencial, al.Ambiente, al.ParametrosEnviados,
                al.EsExitoso, al.MensajeError, al.TiempoEjecucionMs, al.DireccionIP, al.FechaEjecucion,
                a.NombreAPI,
                c.Nombre as NombreCredencial,
                ta.Codigo as TipoAutenticacion
            FROM AuditLogs al
            INNER JOIN APIs a ON al.IdAPI = a.IdAPI
            LEFT JOIN CredencialesAPI c ON al.IdCredencial = c.IdCredencial
            LEFT JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE (@IdAPI IS NULL OR al.IdAPI = @IdAPI)
            AND al.FechaEjecucion BETWEEN @FechaDesde AND @FechaHasta
            ORDER BY al.FechaEjecucion DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        var offset = (pageNumber - 1) * pageSize;

        var logs = await _dbService.QueryAsync<AuditLog>(sql, new
        {
            IdAPI = idAPI,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta,
            Offset = offset,
            PageSize = pageSize
        });

        _logger.LogInformation("Obtenidos {Count} logs de auditoría para API {IdAPI}", logs.Count(), idAPI);
        return logs.ToList();
    }

    public async Task<List<AuditLog>> GetRecentErrorsAsync(int count = 10)
    {
        const string sql = @"
            SELECT TOP (@Count)
                al.IdAPI, al.IdCredencial, al.Ambiente, al.ParametrosEnviados,
                al.EsExitoso, al.MensajeError, al.TiempoEjecucionMs, al.DireccionIP, al.FechaEjecucion,
                a.NombreAPI,
                c.Nombre as NombreCredencial,
                ta.Codigo as TipoAutenticacion
            FROM AuditLogs al
            INNER JOIN APIs a ON al.IdAPI = a.IdAPI
            LEFT JOIN CredencialesAPI c ON al.IdCredencial = c.IdCredencial
            LEFT JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE al.EsExitoso = 0
            ORDER BY al.FechaEjecucion DESC";

        var errors = await _dbService.QueryAsync<AuditLog>(sql, new { Count = count });

        _logger.LogInformation("Obtenidos {Count} errores recientes", errors.Count());
        return errors.ToList();
    }

    public async Task<List<UsageStatistics>> GetUsageStatisticsAsync(int? idAPI = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);
        fechaHasta ??= DateTime.Now;

        const string sql = @"
            SELECT 
                a.IdAPI,
                a.NombreAPI,
                COUNT(*) as TotalEjecuciones,
                SUM(CASE WHEN al.EsExitoso = 1 THEN 1 ELSE 0 END) as EjecucionesExitosas,
                SUM(CASE WHEN al.EsExitoso = 0 THEN 1 ELSE 0 END) as EjecucionesFallidas,
                AVG(CAST(al.TiempoEjecucionMs AS FLOAT)) as TiempoPromedioMs,
                MIN(al.FechaEjecucion) as PrimeraEjecucion,
                MAX(al.FechaEjecucion) as UltimaEjecucion,
                COUNT(DISTINCT al.IdCredencial) as CredencialesUnicas,
                COUNT(DISTINCT al.DireccionIP) as IPsUnicas
            FROM APIs a
            INNER JOIN AuditLogs al ON a.IdAPI = al.IdAPI
            WHERE (@IdAPI IS NULL OR a.IdAPI = @IdAPI)
            AND al.FechaEjecucion BETWEEN @FechaDesde AND @FechaHasta
            GROUP BY a.IdAPI, a.NombreAPI
            ORDER BY TotalEjecuciones DESC";

        var stats = await _dbService.QueryAsync<UsageStatistics>(sql, new
        {
            IdAPI = idAPI,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        });

        _logger.LogInformation("Obtenidas estadísticas de uso para {Count} APIs", stats.Count());
        return stats.ToList();
    }

    // =====================================================
    // MÉTODO PRINCIPAL DE LOGGING ACTUALIZADO
    // =====================================================

    public async Task LogAuditoriaAsync(int idAPI, int? idCredencial, string ambiente, string? parametrosEnviados, bool esExitoso, string? mensajeError, int tiempoEjecucionMs, string? direccionIP)
    {
        try
        {
            const string sql = @"
                INSERT INTO AuditLogs (
                    IdAPI, IdCredencial, Ambiente, ParametrosEnviados, 
                    EsExitoso, MensajeError, TiempoEjecucionMs, DireccionIP
                )
                VALUES (
                    @IdAPI, @IdCredencial, @Ambiente, @ParametrosEnviados,
                    @EsExitoso, @MensajeError, @TiempoEjecucionMs, @DireccionIP
                )";

            await _dbService.ExecuteAsync(sql, new
            {
                IdAPI = idAPI,
                IdCredencial = idCredencial,
                Ambiente = ambiente,
                ParametrosEnviados = parametrosEnviados,
                EsExitoso = esExitoso,
                MensajeError = mensajeError,
                TiempoEjecucionMs = tiempoEjecucionMs,
                DireccionIP = direccionIP
            });

            _logger.LogDebug("Log de auditoría registrado: API={IdAPI}, Credencial={IdCredencial}, Exitoso={EsExitoso}",
                idAPI, idCredencial, esExitoso);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando log de auditoría para API {IdAPI}", idAPI);
            // No propagamos la excepción para evitar afectar el flujo principal
        }
    }

    public async Task<long> GetTotalExecutionsAsync(DateTime? fechaDesde = null)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);

        const string sql = @"
            SELECT COUNT(*) 
            FROM AuditLogs 
            WHERE FechaEjecucion >= @FechaDesde";

        // CORREGIDO: Usar método específico para long
        var total = await _dbService.QueryFirstOrDefaultAsLongAsync(sql, new { FechaDesde = fechaDesde });

        return total;
    }

    public async Task<double> GetAverageExecutionTimeAsync(int? idAPI = null, DateTime? fechaDesde = null)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);

        const string sql = @"
            SELECT AVG(CAST(TiempoEjecucionMs AS FLOAT))
            FROM AuditLogs 
            WHERE (@IdAPI IS NULL OR IdAPI = @IdAPI)
            AND FechaEjecucion >= @FechaDesde
            AND EsExitoso = 1";

        // CORREGIDO: Usar método específico para double
        var average = await _dbService.QueryFirstOrDefaultAsDoubleAsync(sql, new { IdAPI = idAPI, FechaDesde = fechaDesde });

        return average;
    }

    public async Task<double> GetSuccessRateAsync(int? idAPI = null, DateTime? fechaDesde = null)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);

        const string sql = @"
            SELECT 
                CASE 
                    WHEN COUNT(*) = 0 THEN 0
                    ELSE CAST(SUM(CASE WHEN EsExitoso = 1 THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*) * 100
                END as SuccessRate
            FROM AuditLogs 
            WHERE (@IdAPI IS NULL OR IdAPI = @IdAPI)
            AND FechaEjecucion >= @FechaDesde";

        // CORREGIDO: Usar método específico para double
        var successRate = await _dbService.QueryFirstOrDefaultAsDoubleAsync(sql, new { IdAPI = idAPI, FechaDesde = fechaDesde });

        return successRate;
    }

    // =====================================================
    // NUEVOS MÉTODOS PARA ANÁLISIS POR TIPO DE AUTENTICACIÓN
    // =====================================================

    public async Task<List<AuditLog>> GetAuditLogsByAuthTypeAsync(TipoAutenticacion tipoAuth, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);
        fechaHasta ??= DateTime.Now;

        const string sql = @"
            SELECT 
                al.IdAPI, al.IdCredencial, al.Ambiente, al.ParametrosEnviados,
                al.EsExitoso, al.MensajeError, al.TiempoEjecucionMs, al.DireccionIP, al.FechaEjecucion,
                a.NombreAPI,
                c.Nombre as NombreCredencial,
                ta.Codigo as TipoAutenticacion
            FROM AuditLogs al
            INNER JOIN APIs a ON al.IdAPI = a.IdAPI
            LEFT JOIN CredencialesAPI c ON al.IdCredencial = c.IdCredencial
            LEFT JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE ta.Codigo = @TipoAuth
            AND al.FechaEjecucion BETWEEN @FechaDesde AND @FechaHasta
            ORDER BY al.FechaEjecucion DESC";

        var logs = await _dbService.QueryAsync<AuditLog>(sql, new
        {
            TipoAuth = tipoAuth.ToString(),
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        });

        _logger.LogInformation("Obtenidos {Count} logs de auditoría para tipo {TipoAuth}", logs.Count(), tipoAuth);
        return logs.ToList();
    }

    public async Task<Dictionary<string, int>> GetExecutionsByAuthTypeAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);
        fechaHasta ??= DateTime.Now;

        const string sql = @"
            SELECT 
                ISNULL(ta.Codigo, 'NONE') as TipoAuth,
                COUNT(*) as Count
            FROM AuditLogs al
            INNER JOIN APIs a ON al.IdAPI = a.IdAPI
            LEFT JOIN CredencialesAPI c ON al.IdCredencial = c.IdCredencial
            LEFT JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE al.FechaEjecucion BETWEEN @FechaDesde AND @FechaHasta
            GROUP BY ta.Codigo
            ORDER BY Count DESC";

        var results = await _dbService.QueryAsync<dynamic>(sql, new
        {
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        });

        var distribution = results.ToDictionary(r => (string)r.TipoAuth, r => (int)r.Count);

        _logger.LogInformation("Obtenida distribución de ejecuciones por tipo de auth: {Count} tipos", distribution.Count);
        return distribution;
    }

    public async Task<List<AuditLog>> GetAuditLogsByCredentialAsync(int idCredencial, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);
        fechaHasta ??= DateTime.Now;

        const string sql = @"
            SELECT 
                al.IdAPI, al.IdCredencial, al.Ambiente, al.ParametrosEnviados,
                al.EsExitoso, al.MensajeError, al.TiempoEjecucionMs, al.DireccionIP, al.FechaEjecucion,
                a.NombreAPI,
                c.Nombre as NombreCredencial,
                ta.Codigo as TipoAutenticacion
            FROM AuditLogs al
            INNER JOIN APIs a ON al.IdAPI = a.IdAPI
            INNER JOIN CredencialesAPI c ON al.IdCredencial = c.IdCredencial
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE al.IdCredencial = @IdCredencial
            AND al.FechaEjecucion BETWEEN @FechaDesde AND @FechaHasta
            ORDER BY al.FechaEjecucion DESC";

        var logs = await _dbService.QueryAsync<AuditLog>(sql, new
        {
            IdCredencial = idCredencial,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        });

        _logger.LogInformation("Obtenidos {Count} logs de auditoría para credencial {IdCredencial}", logs.Count(), idCredencial);
        return logs.ToList();
    }

    public async Task<double> GetSuccessRateByAuthTypeAsync(TipoAutenticacion tipoAuth, DateTime? fechaDesde = null)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);

        const string sql = @"
            SELECT 
                CASE 
                    WHEN COUNT(*) = 0 THEN 0
                    ELSE CAST(SUM(CASE WHEN al.EsExitoso = 1 THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*) * 100
                END as SuccessRate
            FROM AuditLogs al
            INNER JOIN CredencialesAPI c ON al.IdCredencial = c.IdCredencial
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            WHERE ta.Codigo = @TipoAuth
            AND al.FechaEjecucion >= @FechaDesde";

        // CORREGIDO: Usar método específico para double
        var successRate = await _dbService.QueryFirstOrDefaultAsDoubleAsync(sql, new
        {
            TipoAuth = tipoAuth.ToString(),
            FechaDesde = fechaDesde
        });

        return successRate;
    }

    // =====================================================
    // MÉTODOS DE LIMPIEZA Y MANTENIMIENTO
    // =====================================================

    public async Task<int> CleanupOldAuditLogsAsync(int daysToKeep = 90)
    {
        const string sql = @"
            DELETE FROM AuditLogs 
            WHERE FechaEjecucion < DATEADD(DAY, -@DaysToKeep, GETDATE())";

        var deletedRows = await _dbService.ExecuteAsync(sql, new { DaysToKeep = daysToKeep });

        _logger.LogInformation("Limpieza de logs de auditoría: {Count} registros eliminados", deletedRows);
        return deletedRows;
    }

    // =====================================================
    // MÉTODOS AUXILIARES PARA ESTADÍSTICAS AVANZADAS
    // =====================================================

    public async Task<List<CredentialUsageStatsDto>> GetCredentialUsageStatsAsync(int? idAPI = null, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        fechaDesde ??= DateTime.Now.AddDays(-30);
        fechaHasta ??= DateTime.Now;

        const string sql = @"
            SELECT 
                c.IdCredencial,
                c.Nombre as NombreCredencial,
                c.IdAPI,
                a.NombreAPI,
                ta.Codigo as TipoAutenticacion,
                COUNT(*) as TotalUsos,
                SUM(CASE WHEN al.EsExitoso = 1 THEN 1 ELSE 0 END) as UsosExitosos,
                SUM(CASE WHEN al.EsExitoso = 0 THEN 1 ELSE 0 END) as UsosFallidos,
                AVG(CAST(al.TiempoEjecucionMs AS FLOAT)) as TiempoPromedioMs,
                MIN(al.FechaEjecucion) as PrimerUso,
                MAX(al.FechaEjecucion) as UltimoUso,
                COUNT(DISTINCT al.DireccionIP) as IPsUnicas
            FROM CredencialesAPI c
            INNER JOIN APIs a ON c.IdAPI = a.IdAPI
            INNER JOIN TiposAutenticacion ta ON c.IdTipoAuth = ta.IdTipoAuth
            LEFT JOIN AuditLogs al ON c.IdCredencial = al.IdCredencial 
                AND al.FechaEjecucion BETWEEN @FechaDesde AND @FechaHasta
            WHERE c.EsActivo = 1
            AND (@IdAPI IS NULL OR c.IdAPI = @IdAPI)
            GROUP BY c.IdCredencial, c.Nombre, c.IdAPI, a.NombreAPI, ta.Codigo
            HAVING COUNT(*) > 0
            ORDER BY TotalUsos DESC";

        var stats = await _dbService.QueryAsync<CredentialUsageStatsDto>(sql, new
        {
            IdAPI = idAPI,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        });

        _logger.LogInformation("Obtenidas estadísticas de uso para {Count} credenciales", stats.Count());
        return stats.ToList();
    }

    // =====================================================
    // MÉTODO PARA VERIFICAR SALUD DEL SISTEMA
    // =====================================================

    public async Task<bool> IsSystemHealthyAsync()
    {
        try
        {
            // Verificar conectividad básica
            await _dbService.TestConnectionAsync();

            // Verificar que no hay errores críticos recientes
            var recentErrors = await GetRecentErrorsAsync(5);
            var errorRate = recentErrors.Count;

            return errorRate < 3; // Menos de 3 errores recientes = saludable
        }
        catch
        {
            return false;
        }
    }
}

// =====================================================
// DTOS AUXILIARES CORREGIDOS
// =====================================================

public class CredentialUsageStatsDto
{
    public int IdCredencial { get; set; }
    public string NombreCredencial { get; set; } = string.Empty;
    public int IdAPI { get; set; }
    public string NombreAPI { get; set; } = string.Empty;
    public string TipoAutenticacion { get; set; } = string.Empty;
    public int TotalUsos { get; set; }
    public int UsosExitosos { get; set; }
    public int UsosFallidos { get; set; }
    public double TiempoPromedioMs { get; set; }
    public DateTime? PrimerUso { get; set; }
    public DateTime? UltimoUso { get; set; }
    public int IPsUnicas { get; set; }
    public double TasaExito => TotalUsos > 0 ? (double)UsosExitosos / TotalUsos * 100 : 0;
}