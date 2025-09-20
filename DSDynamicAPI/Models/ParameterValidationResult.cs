
// =====================================================
// MODELOS DE FUNCIONES ESPECÍFICAS
// =====================================================

// Resultado de validación de parámetros
public class ParameterValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object?> ValidatedParameters { get; set; } = new();
}

// Información de conexión de base de datos
public class DatabaseConnectionInfo
{
    public string ServerName { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public TimeSpan ConnectionTime { get; set; }
    public DateTime LastTestedAt { get; set; }
}

// Resultado de prueba de API
public class ApiTestResult
{
    public int IdAPI { get; set; }
    public string NombreAPI { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public int ExecutionTimeMs { get; set; }
    public object? TestData { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    public string Environment { get; set; } = string.Empty;
}