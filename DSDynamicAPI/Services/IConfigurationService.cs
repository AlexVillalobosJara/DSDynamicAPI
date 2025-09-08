// Interfaz para servicio de configuraci�n
public interface IConfigurationService
{
    Task<ApiConfiguration?> GetApiConfigurationAsync(int idApi, string? token = null);
    Task<List<ApiInfo>> GetAvailableApisAsync();
}