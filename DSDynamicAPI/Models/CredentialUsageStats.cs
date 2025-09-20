// Estadísticas de credenciales
public class CredentialUsageStats
{
    public int IdCredencial { get; set; }
    public string NombreCredencial { get; set; } = string.Empty;
    public string TipoAuth { get; set; } = string.Empty;
    public int TotalUses { get; set; }
    public int SuccessfulUses { get; set; }
    public int FailedUses { get; set; }
    public DateTime? LastUsed { get; set; }
    public DateTime? FirstUsed { get; set; }
    public double SuccessRate => TotalUses > 0 ? (double)SuccessfulUses / TotalUses * 100 : 0;
}
