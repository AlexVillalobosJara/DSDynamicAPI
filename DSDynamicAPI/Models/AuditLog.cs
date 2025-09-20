// Log de auditoría (ACTUALIZADO)
public class AuditLog
{
    public int IdAPI { get; set; }
    public int? IdCredencial { get; set; }
    public string Ambiente { get; set; } = string.Empty;
    public string? ParametrosEnviados { get; set; }
    public bool EsExitoso { get; set; }
    public string? MensajeError { get; set; }
    public int TiempoEjecucionMs { get; set; }
    public string? DireccionIP { get; set; }
    public DateTime FechaEjecucion { get; set; }

    // Propiedades de navegación
    public string NombreAPI { get; set; } = string.Empty;
    public string? NombreCredencial { get; set; }
    public string? TipoAutenticacion { get; set; }

    // Propiedades calculadas
    public string EstadoTexto => EsExitoso ? "Exitoso" : "Fallido";
    public string BadgeClass => EsExitoso ? "badge-success" : "badge-danger";
    public string TiempoEjecucionTexto => $"{TiempoEjecucionMs}ms";
    public string FechaEjecucionTexto => FechaEjecucion.ToString("dd/MM/yyyy HH:mm:ss");
}
