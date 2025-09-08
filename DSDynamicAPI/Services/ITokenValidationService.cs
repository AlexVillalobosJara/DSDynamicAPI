// Interfaz para validación de tokens
public interface ITokenValidationService
{
    Task<TokenValidationResult> ValidateTokenAsync(string token);
}