
// Configuración JWT
using System.ComponentModel.DataAnnotations;

public class JWTConfiguration
{
    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    [MinLength(32, ErrorMessage = "La clave secreta debe tener al menos 32 caracteres")]
    public string SecretKey { get; set; } = string.Empty;

    public bool ValidateLifetime { get; set; } = true;
    public int ClockSkewSeconds { get; set; } = 300;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateIssuerSigningKey { get; set; } = true;
}

// Configuración OAuth2
public class OAuth2Configuration
{
    [Required]
    [Url]
    public string AuthorizationServer { get; set; } = string.Empty;

    [Required]
    [Url]
    public string TokenEndpoint { get; set; } = string.Empty;

    [Url]
    public string? IntrospectionEndpoint { get; set; }

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    public List<string> RequiredScopes { get; set; } = new();
    public int TokenCacheMinutes { get; set; } = 5;
}

// Configuración NTLM
public class NTLMConfiguration
{
    public string? Domain { get; set; }
    public List<string> RequiredGroups { get; set; } = new();
    public List<string> AllowedUsers { get; set; } = new();
    public bool RequireAuthentication { get; set; } = true;
}

// Configuración Basic Auth
public class BasicAuthConfiguration
{
    [Required]
    public List<BasicAuthUser> Users { get; set; } = new();

    public bool RequireHttps { get; set; } = true;
    public int MaxFailedAttempts { get; set; } = 3;
    public int LockoutMinutes { get; set; } = 15;
}

public class BasicAuthUser
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty; // Debe ser un hash, no texto plano

    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime? LastLogin { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockoutUntil { get; set; }
}