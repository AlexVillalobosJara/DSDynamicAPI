// Opciones de configuración de base de datos
public class DatabaseOptions
{
    public string ConfigConnectionString { get; set; } = string.Empty;
    public int DefaultCommandTimeout { get; set; } = 30;
}