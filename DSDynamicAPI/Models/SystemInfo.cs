
// =====================================================
// MODELOS DE RESPUESTA DEL SISTEMA
// =====================================================

// Información del sistema
public class SystemInfo
{
    public string ApplicationName { get; set; } = "Dynamic API";
    public string Version { get; set; } = "2.0.0";
    public string Environment { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public TimeSpan Uptime { get; set; }
    public List<AuthTypeInfo> SupportedAuthTypes { get; set; } = new();
    public SystemHealth Health { get; set; } = new();
}

public class AuthTypeInfo
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HeaderExample { get; set; } = string.Empty;
    public bool RequiresConfiguration { get; set; }
    public bool IsActive { get; set; }
}

public class SystemHealth
{
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<HealthCheck> Checks { get; set; } = new();
    public DateTime LastCheck { get; set; }
}

public class HealthCheck
{
    public string Name { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TimeSpan ResponseTime { get; set; }
}