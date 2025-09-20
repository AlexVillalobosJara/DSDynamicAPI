// Resultado de validación de autenticación
public class AuthValidationResult
{
    public bool IsValid { get; set; }
    public bool RateLimitExceeded { get; set; }
    public int IdAPI { get; set; }
    public int? IdCredencial { get; set; }
    public string TipoAuth { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int RemainingRequests { get; set; }
    public DateTime? ResetTime { get; set; }
    public string? ConfigAuth { get; set; }
    public Dictionary<string, object>? AuthMetadata { get; set; }
}