public class AuthUsageStatisticsDto
{
    public TipoAutenticacion TipoAuth { get; set; }
    public string NombreTipoAuth { get; set; } = string.Empty;
    public int TotalAPIs { get; set; }
    public int TotalCredentials { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public double SuccessRate { get; set; }
    public double AverageExecutionTimeMs { get; set; }
    public DateTime? FirstExecution { get; set; }
    public DateTime? LastExecution { get; set; }

    // Propiedades calculadas
    public string SuccessRateText => $"{SuccessRate:F1}%";
    public string AverageExecutionTimeText => $"{AverageExecutionTimeMs:F1}ms";
    public string TipoAuthText => TipoAuth.ToString();
}