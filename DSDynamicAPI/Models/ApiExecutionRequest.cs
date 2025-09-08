// Request para ejecutar API
using System.ComponentModel.DataAnnotations;

public class ApiExecutionRequest
{
    [Required]
    public int IdAPI { get; set; }

    [Required]
    public string Environment { get; set; } = "PRODUCTION";

    public Dictionary<string, object?> Parameters { get; set; } = new();
}