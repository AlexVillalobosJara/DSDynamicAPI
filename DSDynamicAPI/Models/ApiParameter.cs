// Parámetros de API
public class ApiParameter
{
    public string NombreParametro { get; set; } = string.Empty;
    public string TipoParametro { get; set; } = string.Empty;
    public bool EsObligatorio { get; set; }
    public string? ValorPorDefecto { get; set; }
    public int Orden { get; set; }
    public string? Descripcion { get; set; }
}