
// Response de ejecución (ACTUALIZADO)
public class ApiExecutionResponse
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Message { get; set; }
    public int ExecutionTimeMs { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    // NUEVOS CAMPOS
    public string? AuthType { get; set; }
    public int? CredentialId { get; set; }
    public string Environment { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}