
// Información de API disponible (ACTUALIZADO)
public class ApiInfo
{
    public int IdAPI { get; set; }
    public string NombreAPI { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string TipoObjeto { get; set; } = string.Empty;
    public string TipoAutenticacion { get; set; } = string.Empty;
    public string NombreTipoAuth { get; set; } = string.Empty;
    public List<ParameterInfo> Parametros { get; set; } = new();
    public string Endpoint { get; set; } = string.Empty;
    public string ExampleCall { get; set; } = string.Empty;
    public string AuthHeaderExample { get; set; } = string.Empty;
}