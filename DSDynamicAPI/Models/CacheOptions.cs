
// =====================================================
// MODELOS DE CONFIGURACIÓN AVANZADA
// =====================================================

// Configuración de cache
public class CacheOptions
{
    public bool EnableCaching { get; set; } = true;
    public int DefaultExpirationMinutes { get; set; } = 5;
    public int MaxCacheSize { get; set; } = 1000;
    public bool EnableDistributedCache { get; set; } = false;
    public string? RedisConnectionString { get; set; }
}

// Configuración de performance
public class PerformanceOptions
{
    public bool EnableResponseCompression { get; set; } = true;
    public bool EnableResponseCaching { get; set; } = true;
    public int MaxRequestSizeBytes { get; set; } = 10485760; // 10MB
    public int RequestTimeoutSeconds { get; set; } = 30;
    public bool EnableQueryOptimization { get; set; } = true;
}

// Configuración de monitoreo
public class MonitoringOptions
{
    public bool EnableDetailedMetrics { get; set; } = true;
    public int MetricsRetentionDays { get; set; } = 30;
    public bool EnableHealthChecks { get; set; } = true;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public bool EnableAlerts { get; set; } = true;
    public List<string> AlertRecipients { get; set; } = new();
}
