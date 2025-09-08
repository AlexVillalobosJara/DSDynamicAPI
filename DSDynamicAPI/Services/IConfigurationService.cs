// Interfaz para servicio de configuración
public interface IConfigurationService
{
    Task<ApiConfiguration?> GetApiConfigurationAsync(int idApi, string? token = null);
    Task<List<ApiInfo>> GetAvailableApisAsync();
}