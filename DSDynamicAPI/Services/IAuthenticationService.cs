
// =====================================================
// IAuthenticationService - NUEVA (reemplaza ITokenValidationService)
// =====================================================
public interface IAuthenticationService
{
    // Validación principal de autenticación
    Task<AuthValidationResult> AuthenticateAsync(CredentialValidationRequest request);
    Task<AuthValidationResult> ValidateTokenAsync(string token, int? idAPI = null);
    Task<AuthValidationResult> ValidateApiKeyAsync(string apiKey, int? idAPI = null);
    Task<AuthValidationResult> ValidateJWTAsync(string jwt, int idAPI);
    Task<AuthValidationResult> ValidateOAuth2TokenAsync(string token, int idAPI);
    Task<AuthValidationResult> ValidateNTLMAsync(string credentials, int idAPI);
    Task<AuthValidationResult> ValidateBasicAuthAsync(string credentials, int idAPI);

    // Rate limiting y control de acceso
    Task<bool> CheckRateLimitAsync(int idCredencial, int idAPI);
    Task<bool> IncrementRateLimitAsync(int idCredencial);
    Task<Dictionary<int, int>> GetCurrentRateLimitsAsync(List<int> credencialIds);

    // NUEVO: Verificación de acceso por credencial
    Task<bool> VerifyCredentialAccessAsync(int idCredencial, int idAPI);
    Task<bool> VerifyCredentialActiveAsync(int idCredencial);
    Task<bool> VerifyAPIActiveAsync(int idAPI);

    // Configuración y metadatos
    Task<ApiConfiguration?> GetAPIConfigForAuthAsync(int idAPI);
    Task<bool> IsAPIPublicAsync(int idAPI); // Para APIs tipo NONE
    Task<string> GetRequiredAuthTypeAsync(int idAPI);

    // Auditoría de autenticación
    Task LogAuthenticationAttemptAsync(int idAPI, int? idCredencial, bool isSuccessful, string? errorMessage, string? ipAddress, string ambiente);
    Task<List<AuditLog>> GetFailedAuthAttemptsAsync(DateTime? fechaDesde = null, int count = 100);

    // Estadísticas y monitoreo
    Task<AuthStatsDto> GetAuthStatsAsync(int idAPI);
    Task<List<ActivityDto>> GetRecentActivityAsync(int idAPI, int count = 10);
    Task<SystemAuthHealthDto> GetSystemAuthHealthAsync();
}