public class TipoAutenticacionDto
{
    public int IdTipoAuth { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool RequiereConfiguracion { get; set; }
    public bool EsActivo { get; set; }
    public DateTime FechaCreacion { get; set; }

    // Propiedades calculadas
    public string EstadoTexto => EsActivo ? "Activo" : "Inactivo";
    public string BadgeClass => EsActivo ? "badge-success" : "badge-secondary";
}