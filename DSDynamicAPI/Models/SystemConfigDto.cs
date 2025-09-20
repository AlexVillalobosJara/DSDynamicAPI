public class SystemConfigDto
{
    public int IdConfig { get; set; }
    public string StringConexionTest { get; set; } = string.Empty;
    public string StringConexionProduccion { get; set; } = string.Empty;
    public int TimeoutEjecucionSegundos { get; set; } = 30;
    public string UrlBaseDinamica { get; set; } = string.Empty;
    public bool RequiereHttps { get; set; } = true;
    public int RateLimitGlobal { get; set; } = 1000;
    public string? ConfiguracionAdicional { get; set; }
    public DateTime FechaModificacion { get; set; }
    public string ModificadoPor { get; set; } = string.Empty;
}