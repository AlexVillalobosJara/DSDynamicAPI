
// Request para validación de credenciales
using System.ComponentModel.DataAnnotations;

public class CredentialValidationRequest
{
    [Required]
    public int IdAPI { get; set; }

    [Required]
    public TipoAutenticacion TipoAuth { get; set; }

    public string? Credential { get; set; }
    public string Environment { get; set; } = "PRODUCTION";
    public Dictionary<string, string> Headers { get; set; } = new();
    public string? IPAddress { get; set; }
}

// Resultado de validación de autenticación
//public class AuthValidationResult
//{
//    public bool IsValid { get; set; }
//    public bool RateLimitExceeded { get; set; }
//    public int IdAPI { get; set; }
//    public int? IdCredencial { get; set; }
//    public string TipoAuth { get; set; } = string.Empty;
//    public string? ErrorMessage { get; set; }
//    public int RemainingRequests { get; set; }
//    public DateTime? ResetTime { get; set; }
//    public string? ConfigAuth { get; set; }
//    public Dictionary<string, object> AuthMetadata { get; set; } = new();
//}

// Estadísticas de autenticación
public class AuthStatsDto
{
    public int IdAPI { get; set; }
    public string NombreAPI { get; set; } = string.Empty;
    public TipoAutenticacion TipoAuth { get; set; }
    public string NombreTipoAuth { get; set; } = string.Empty;
    public int TotalCredenciales { get; set; }
    public int CredencialesActivas { get; set; }
    public int CredencialesExpiradas { get; set; }
    public int AuthenticationsToday { get; set; }
    public int FailedAuthsToday { get; set; }
    public double SuccessRate { get; set; }
    public DateTime? LastAuthentication { get; set; }

    // Propiedades calculadas
    public string SuccessRateText => $"{SuccessRate:F1}%";
    public string LastAuthenticationText => LastAuthentication?.ToString("dd/MM/yyyy HH:mm") ?? "Nunca";
}

// Actividad reciente del sistema de auth
public class ActivityDto
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty; // AUTH_SUCCESS, AUTH_FAILED, CREDENTIAL_CREATED, etc.
    public int IdAPI { get; set; }
    public int? IdCredencial { get; set; }
    public string? Description { get; set; }
    public string? IPAddress { get; set; }

    // Propiedades calculadas
    public string TimestampText => Timestamp.ToString("dd/MM/yyyy HH:mm:ss");
    public string EventTypeText => EventType switch
    {
        "AUTH_SUCCESS" => "Autenticación exitosa",
        "AUTH_FAILED" => "Autenticación fallida",
        "CREDENTIAL_CREATED" => "Credencial creada",
        "CREDENTIAL_EXPIRED" => "Credencial expirada",
        "CREDENTIAL_REVOKED" => "Credencial revocada",
        _ => EventType
    };
}

// Estado de salud del sistema de autenticación
public class SystemAuthHealthDto
{
    public bool IsHealthy { get; set; }
    public int TotalAPIs { get; set; }
    public int APIsWithAuth { get; set; }
    public int TotalCredentials { get; set; }
    public int ActiveCredentials { get; set; }
    public int ExpiredCredentials { get; set; }
    public int CredentialsExpiringSoon { get; set; }
    public Dictionary<TipoAutenticacion, int> AuthTypeDistribution { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    // Propiedades calculadas
    public string HealthStatus => IsHealthy ? "Saludable" : (Errors.Any() ? "Crítico" : "Advertencia");
    public string HealthBadgeClass => IsHealthy ? "badge-success" : (Errors.Any() ? "badge-danger" : "badge-warning");
}

// Estadísticas de credenciales específicas
public class CredentialStatsDto
{
    public int IdCredencial { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public TipoAutenticacion TipoAuth { get; set; }
    public int UsesToday { get; set; }
    public int UsesThisMonth { get; set; }
    public int TotalUses { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LastUsed { get; set; }
    public DateTime? LastFailed { get; set; }
    public bool IsNearRateLimit { get; set; }
    public bool IsExpiringSoon { get; set; }

    // Propiedades calculadas
    public string LastUsedText => LastUsed?.ToString("dd/MM/yyyy HH:mm") ?? "Nunca";
    public string LastFailedText => LastFailed?.ToString("dd/MM/yyyy HH:mm") ?? "Nunca";
    public string TipoAuthText => TipoAuth.ToString();
}