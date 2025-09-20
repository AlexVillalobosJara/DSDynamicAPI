// Respuesta de error de autenticación
public class AuthenticationErrorResponse : ErrorResponse
{
    public string RequiredAuthType { get; set; } = string.Empty;
    public string AuthHeaderExample { get; set; } = string.Empty;
    public List<string> SupportedAuthTypes { get; set; } = new();
    public bool IsPublicAPI { get; set; }
}