using System.Text.Json;

public class CredencialAPIDto
{
    public int IdCredencial { get; set; }
    public int IdAPI { get; set; }
    public string TipoAutenticacion { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string ValorCredencial { get; set; } = string.Empty;
    public string? ConfiguracionExtra { get; set; }
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaExpiracion { get; set; }
    public DateTime? UltimoUso { get; set; }
    public int ContadorUsos { get; set; }
    public bool EsActivo { get; set; }
    public string CreadoPor { get; set; } = string.Empty;

    // Propiedades de navegación
    public string NombreTipoAuth { get; set; } = string.Empty;
    public string? NombreAPI { get; set; }

    // Propiedades calculadas
    public bool EstaExpirada { get; set; }
    public int? DaysUntilExpiration { get; set; }
    public string ValorEnmascarado { get; set; } = string.Empty;
    public string EstadoTexto { get; set; } = string.Empty;
    public string FechaExpiracionTexto { get; set; } = string.Empty;
    public string UltimoUsoTexto { get; set; } = string.Empty;

    public string BadgeClass => EsActivo
        ? (EstaExpirada ? "badge-danger" : "badge-success")
        : "badge-secondary";

    // Métodos para manejar configuración JSON
    public T? GetConfiguracionExtra<T>() where T : class
    {
        if (string.IsNullOrWhiteSpace(ConfiguracionExtra))
            return null;

        try
        {
            return JsonSerializer.Deserialize<T>(ConfiguracionExtra);
        }
        catch
        {
            return null;
        }
    }

    public void SetConfiguracionExtra<T>(T configuracion) where T : class
    {
        ConfiguracionExtra = configuracion != null
            ? JsonSerializer.Serialize(configuracion)
            : null;
    }
}