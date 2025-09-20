
// Request para ejecutar API (ACTUALIZADO)
using System.ComponentModel.DataAnnotations;

public class ApiExecutionRequest
{
    [Required]
    public int IdAPI { get; set; }

    [Required]
    public string Environment { get; set; } = "PRODUCTION";

    public Dictionary<string, object?> Parameters { get; set; } = new();

    // NUEVOS CAMPOS PARA AUTENTICACIÓN
    public string? AuthType { get; set; }
    public string? Credential { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
}