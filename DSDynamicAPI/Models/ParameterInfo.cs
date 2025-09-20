
// Información de parámetro para documentación
public class ParameterInfo
{
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public bool Requerido { get; set; }
    public string? ValorPorDefecto { get; set; }
    public string? Descripcion { get; set; }
}