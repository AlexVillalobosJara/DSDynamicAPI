// Estadísticas de uso
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
    public double TasaExito => TotalEjecuciones > 0 ? (double)EjecucionesExitosas / TotalEjecuciones * 100 : 0;
}