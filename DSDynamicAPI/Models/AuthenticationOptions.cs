
// Opciones de autenticación
public class AuthenticationOptions
{
    public JWTOptions JWT { get; set; } = new();
    public OAuth2Options OAuth2 { get; set; } = new();
    public RateLimitOptions RateLimit { get; set; } = new();
    public SecurityOptions Security { get; set; } = new();
}

public class JWTOptions
{
    public string DefaultIssuer { get; set; } = string.Empty;
    public string DefaultAudience { get; set; } = string.Empty;
    public string DefaultSecretKey { get; set; } = string.Empty;
    public int DefaultExpirationMinutes { get; set; } = 60;
    public int ClockSkewSeconds { get; set; } = 300;
}

public class OAuth2Options
{
    public int DefaultTokenCacheMinutes { get; set; } = 5;
    public int DefaultClientTimeoutSeconds { get; set; } = 30;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
}

public class RateLimitOptions
{
    public int DefaultLimitPerMinute { get; set; } = 100;
    public int PublicAPILimit { get; set; } = 50;
    public int AuthenticatedAPILimit { get; set; } = 200;
    public int PremiumAPILimit { get; set; } = 500;
}

public class SecurityOptions
{
    public bool EnableIPWhitelist { get; set; } = false;
    public List<string> AllowedIPs { get; set; } = new();
    public bool BlockSuspiciousRequests { get; set; } = true;
    public int MaxFailedAttemptsPerIP { get; set; } = 10;
    public int BlockDurationMinutes { get; set; } = 15;
    public bool RequireHttps { get; set; } = true;
}
