// Nuevo modelo para errores de autenticación
public class AuthenticationError
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RequiredAuthType { get; set; } = string.Empty;
    public string AuthHeaderExample { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}