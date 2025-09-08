// Resultado de validación de token
public class TokenValidationResult
{
    public bool IsValid { get; set; }
    public bool RateLimitExceeded { get; set; }
    public int IdAPI { get; set; }
    public int IdToken { get; set; }
    public string? ErrorMessage { get; set; }
    public int RemainingRequests { get; set; }
    public DateTime? ResetTime { get; set; }
}