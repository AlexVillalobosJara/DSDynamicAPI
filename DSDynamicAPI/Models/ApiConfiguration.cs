// Configuración de API desde base de datos
public class ApiConfiguration
{
    public int IdAPI { get; set; }
    public int IdToken { get; set; }
    public string NombreAPI { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string ObjetoSQL { get; set; } = string.Empty;
    public string TipoObjeto { get; set; } = string.Empty;
    public bool EsActivo { get; set; }
    public int RateLimitPorMinuto { get; set; }
    public string StringConexionTest { get; set; } = string.Empty;
    public string StringConexionProduccion { get; set; } = string.Empty;
    public int TimeoutEjecucionSegundos { get; set; }
    public List<ApiParameter> Parametros { get; set; } = new();
}