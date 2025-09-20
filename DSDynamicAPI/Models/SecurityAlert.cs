
// =====================================================
// MODELOS DE ALERTAS Y NOTIFICACIONES
// =====================================================

// Alerta de seguridad
public class SecurityAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty; // FAILED_AUTH_SPIKE, HIGH_USAGE, etc.
    public string Severity { get; set; } = string.Empty; // LOW, MEDIUM, HIGH, CRITICAL
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; } // IP, Credential, API, etc.
    public Dictionary<string, object> Details { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

// Notificación del sistema
public class SystemNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty; // INFO, WARNING, ERROR
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public string? ActionUrl { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}