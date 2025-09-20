// Respuesta de error estándar (ACTUALIZADA)
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // NUEVOS CAMPOS
    public string? RequiredAuthType { get; set; }
    public string? AuthHeaderExample { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}