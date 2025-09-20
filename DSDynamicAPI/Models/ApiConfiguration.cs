
// Configuración de API desde base de datos (ACTUALIZADO)
using System.Text.Json;

public class ApiConfiguration
{
    public int IdAPI { get; set; }
    public string NombreAPI { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string ObjetoSQL { get; set; } = string.Empty;
    public string TipoObjeto { get; set; } = string.Empty;
    public bool EsActivo { get; set; }
    public int RateLimitPorMinuto { get; set; }

    // NUEVOS CAMPOS DE AUTENTICACIÓN
    public string TipoAutenticacion { get; set; } = string.Empty;
    public string NombreTipoAuth { get; set; } = string.Empty;
    public string? ConfiguracionAuth { get; set; }

    public string StringConexionTest { get; set; } = string.Empty;
    public string StringConexionProduccion { get; set; } = string.Empty;
    public int TimeoutEjecucionSegundos { get; set; } = 30;
    public List<ApiParameter> Parametros { get; set; } = new();

    // Métodos para manejar configuración de autenticación
    public T? GetConfiguracionAuth<T>() where T : class
    {
        if (string.IsNullOrWhiteSpace(ConfiguracionAuth))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(ConfiguracionAuth);
        }
        catch
        {
            return null;
        }
    }

    public void SetConfiguracionAuth<T>(T configuracion) where T : class
    {
        ConfiguracionAuth = configuracion != null
            ? JsonSerializer.Serialize(configuracion)
            : null;
    }
}