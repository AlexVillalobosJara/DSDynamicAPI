
// =====================================================
// IConfigurationService - ACTUALIZADA
// =====================================================
public interface IConfigurationService
{
    // M�todos existentes actualizados
    Task<ApiConfiguration?> GetApiConfigurationAsync(int idApi, string? credential = null);
    Task<List<ApiInfo>> GetAvailableApisAsync();

    // NUEVOS M�TODOS para sistema multi-auth
    Task<ApiConfiguration?> GetApiConfigurationWithAuthAsync(int idApi, TipoAutenticacion tipoAuth, string? credential = null);
    Task<List<TipoAutenticacionDto>> GetSupportedAuthTypesAsync();
    Task<bool> ValidateAPIAuthConfigAsync(int idApi, TipoAutenticacion tipoAuth);
    Task<string?> GetAuthConfigurationAsync(int idApi);
}