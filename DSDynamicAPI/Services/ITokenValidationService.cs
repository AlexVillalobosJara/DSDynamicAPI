// Interfaz para validaci�n de tokens
public interface ITokenValidationService
{
    Task<TokenValidationResult> ValidateTokenAsync(string token);
}