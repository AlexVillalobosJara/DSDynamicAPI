// Log de auditoría
public class AuditLog
{
    public int IdAPI { get; set; }
    public int? IdToken { get; set; }
    public string Ambiente { get; set; } = string.Empty;
    public string? ParametrosEnviados { get; set; }
    public bool EsExitoso { get; set; }
    public string? MensajeError { get; set; }
    public int TiempoEjecucionMs { get; set; }
    public string? DireccionIP { get; set; }
    public DateTime FechaEjecucion { get; set; } = DateTime.UtcNow;
}