// Estadísticas de uso (ACTUALIZADAS)
public class UsageStatistics
{
    public int IdAPI { get; set; }
    public string NombreAPI { get; set; } = string.Empty;
    public int TotalEjecuciones { get; set; }
    public int EjecucionesExitosas { get; set; }
    public int EjecucionesFallidas { get; set; }
    public double TiempoPromedioMs { get; set; }
    public DateTime? PrimeraEjecucion { get; set; }
    public DateTime? UltimaEjecucion { get; set; }
    public int CredencialesUnicas { get; set; }
    public int IPsUnicas { get; set; }

    // Propiedades calculadas
    public double TasaExito => TotalEjecuciones > 0
        ? (double)EjecucionesExitosas / TotalEjecuciones * 100
        : 0;

    public string TasaExitoTexto => $"{TasaExito:F1}%";
    public string TiempoPromedioTexto => $"{TiempoPromedioMs:F1}ms";
    public string PrimeraEjecucionTexto => PrimeraEjecucion?.ToString("dd/MM/yyyy") ?? "N/A";
    public string UltimaEjecucionTexto => UltimaEjecucion?.ToString("dd/MM/yyyy HH:mm") ?? "N/A";
}