// =====================================================
// ICredencialService - NUEVA (reemplaza ITokenService)
// =====================================================
public interface ICredencialService
{
    // Gestión básica de credenciales
    Task<List<CredencialAPIDto>> GetCredencialsByAPIAsync(int idAPI);
    Task<CredencialAPIDto?> GetCredencialByIdAsync(int idCredencial);
    Task<CredencialAPIDto?> GetCredencialByValueAsync(string valorCredencial);
    Task<List<CredencialAPIDto>> GetAllCredencialesAsync();
    Task<List<CredencialAPIDto>> GetCredencialesByTypeAsync(TipoAutenticacion tipoAuth);

    // Creación y gestión de credenciales
    Task<int> CreateCredencialAsync(CredencialAPIDto credencial);
    Task<bool> UpdateCredencialAsync(CredencialAPIDto credencial);
    Task<bool> DeleteCredencialAsync(int idCredencial);
    Task<bool> ToggleCredencialStatusAsync(int idCredencial);

    // Generación automática de credenciales
    Task<string> GenerateTokenAsync(int idAPI, int diasExpiracion = 365, string? creadoPor = null);
    Task<string> GenerateApiKeyAsync(int idAPI, int? diasExpiracion = null, string? creadoPor = null);
    Task<CredencialAPIDto> CreateJWTCredentialAsync(int idAPI, string valorJWT, object? configuracion = null, int? diasExpiracion = null, string? creadoPor = null);

    // Validación y autenticación
    Task<AuthValidationResult> ValidateCredentialAsync(CredentialValidationRequest request);
    Task<bool> ValidateCredentialSimpleAsync(string valorCredencial, int? idAPI = null);
    Task<bool> CheckRateLimitAsync(int idCredencial);

    // Gestión de expiración
    Task<List<CredencialAPIDto>> GetExpiredCredentialsAsync();
    Task<List<CredencialAPIDto>> GetCredentialsExpiringSoonAsync(int days = 7);
    Task<int> CleanupExpiredCredentialsAsync();
    Task<bool> RevokeCredentialAsync(int idCredencial);
    Task<bool> ExtendCredentialExpirationAsync(int idCredencial, int additionalDays);

    // Estadísticas y monitoreo
    Task<int> GetActiveCredentialsCountAsync(int? idAPI = null);
    Task<List<CredencialAPIDto>> GetCredentialsExceedingLimitAsync();
    Task<Dictionary<TipoAutenticacion, int>> GetCredentialsByTypeStatsAsync();
}