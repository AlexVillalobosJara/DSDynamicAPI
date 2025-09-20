// =====================================================
// Program.cs - CORREGIDO PARA RESOLVER STRING DE CONEXIÓN
// =====================================================

using DynamicAPIs.Services.Database;
using DynamicAPIs.Services.Implementation;
using DynamicAPIs.Services.Interfaces;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// =====================================================
// CONFIGURACIÓN DE LOGGING CON SERILOG
// =====================================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "DynamicAPI")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}")
    .WriteTo.File("logs/dynamicapi-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
        retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

// =====================================================
// SERVICIOS PRINCIPALES
// =====================================================
builder.Services.AddControllers(options =>
{
    // Configurar comportamiento de modelo binding
    options.SuppressAsyncSuffixInActionNames = false;
});

builder.Services.AddEndpointsApiExplorer();

// =====================================================
// CONFIGURACIÓN DE SWAGGER ACTUALIZADA
// =====================================================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Dynamic API",
        Version = "v1",
        Description = "API dinámica con soporte multi-autenticación",
        Contact = new OpenApiContact
        {
            Name = "Soporte Técnico",
            Email = "soporte@empresa.com"
        }
    });

    // Configuración de seguridad para múltiples tipos de autenticación

    // Bearer Token / JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT/Bearer Token. Ejemplo: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // API Key
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key en header. Ejemplo: X-API-Key: {apikey}",
        Name = "X-API-Key",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    // Token personalizado
    c.AddSecurityDefinition("Token", new OpenApiSecurityScheme
    {
        Description = "Token personalizado. Ejemplo: X-API-Token: {token}",
        Name = "X-API-Token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    // Basic Authentication
    c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
    {
        Description = "Autenticación básica. Ejemplo: Authorization: Basic {base64credentials}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "basic"
    });

    // OAuth2
    c.AddSecurityDefinition("OAuth2", new OpenApiSecurityScheme
    {
        Description = "OAuth2 Bearer Token. Ejemplo: Authorization: Bearer {oauth2_token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri("https://auth.example.com/token", UriKind.Absolute),
                Scopes = new Dictionary<string, string>
                {
                    { "api.read", "Leer APIs" },
                    { "api.execute", "Ejecutar APIs" }
                }
            }
        }
    });

    // Requerimiento de seguridad global
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Token"
                }
            },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Basic"
                }
            },
            Array.Empty<string>()
        }
    });

    // Incluir comentarios XML si existen
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// =====================================================
// CONFIGURACIÓN DE BASE DE DATOS - COMPLETAMENTE CORREGIDA
// =====================================================
builder.Services.Configure<DatabaseOptions>(options =>
{
    // Obtener connection string desde appsettings.json
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    // DEBUG: Mostrar configuración encontrada
    Console.WriteLine($"=== DEBUG CONFIGURACIÓN ===");
    Console.WriteLine($"Connection String encontrado: {!string.IsNullOrEmpty(connectionString)}");
    if (!string.IsNullOrEmpty(connectionString))
    {
        var safeConnString = connectionString.Length > 50 ? connectionString.Substring(0, 50) + "..." : connectionString;
        Console.WriteLine($"Connection String: {safeConnString}");
    }

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' no encontrado en configuración");
    }

    // CORRECCIÓN: Usar los nombres correctos de las propiedades
    options.ConfigConnectionString = connectionString;
    options.DefaultCommandTimeout = builder.Configuration.GetValue<int>("DatabaseOptions:DefaultCommandTimeout", 30);
    options.EnableRetry = builder.Configuration.GetValue<bool>("DatabaseOptions:EnableRetry", true); // CORREGIDO
    options.MaxRetryAttempts = builder.Configuration.GetValue<int>("DatabaseOptions:MaxRetryAttempts", 3);
    options.LogQueries = builder.Configuration.GetValue<bool>("DatabaseOptions:LogQueries", false); // CORREGIDO
    options.EnablePerformanceCounters = builder.Configuration.GetValue<bool>("DatabaseOptions:EnablePerformanceCounters", false);

    // DEBUG: Mostrar configuración final
    Console.WriteLine($"ConfigConnectionString configurado: {!string.IsNullOrEmpty(options.ConfigConnectionString)}");
    Console.WriteLine($"DefaultCommandTimeout: {options.DefaultCommandTimeout}");
    Console.WriteLine($"============================");
});

// =====================================================
// REGISTRO DE SERVICIOS ACTUALIZADOS
// =====================================================

// Servicios de base de datos
builder.Services.AddSingleton<DatabaseService>();

// Servicios principales ACTUALIZADOS
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>(); // NUEVO
builder.Services.AddScoped<ICredencialService, CredencialService>(); // NUEVO (reemplaza ITokenValidationService)
builder.Services.AddScoped<ITipoAutenticacionService, TipoAutenticacionService>(); // NUEVO
builder.Services.AddScoped<ISqlExecutionService, SqlExecutionService>();
builder.Services.AddScoped<IAuditService, AuditService>(); // ACTUALIZADO

// HttpClient para servicios que requieren llamadas externas (OAuth2, etc.)
builder.Services.AddHttpClient();

// =====================================================
// CONFIGURACIÓN DE RATE LIMITING AVANZADO - CORREGIDA
// =====================================================
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Política por defecto
    options.AddFixedWindowLimiter("DefaultPolicy", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });

    // Política para APIs públicas (más permisiva)
    options.AddFixedWindowLimiter("PublicAPIPolicy", opt =>
    {
        opt.PermitLimit = 50;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
    });

    // Política para APIs autenticadas (más estricta)
    options.AddFixedWindowLimiter("AuthenticatedAPIPolicy", opt =>
    {
        opt.PermitLimit = 200;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 20;
    });

    // Política dinámica basada en tipo de autenticación - COMPLETAMENTE CORREGIDA
    options.AddPolicy("DynamicAuthPolicy", context =>
    {
        // CORRECCIÓN: Usar context.HttpContext.Items correctamente
        var requestContext = context.Items["RequestContext"] as RequestContext;

        return requestContext?.TipoAuth switch
        {
            "NONE" => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "public",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 50,
                    Window = TimeSpan.FromMinutes(1)
                }),
            "BASIC" => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "basic",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1)
                }),
            "TOKEN" or "APIKEY" => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "token",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1)
                }),
            "JWT" or "OAUTH2" => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "advanced",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 500,
                    Window = TimeSpan.FromMinutes(1)
                }),
            _ => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "default",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1)
                })
        };
    });
});

// =====================================================
// CONFIGURACIÓN DE CORS
// =====================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? new[] { "*" };

        if (allowedOrigins.Contains("*"))
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

// =====================================================
// CONFIGURACIÓN DE COMPRESIÓN Y PERFORMANCE
// =====================================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

// =====================================================
// CONFIGURACIÓN DE HEALTH CHECKS
// =====================================================
builder.Services.AddHealthChecks()
    .AddCheck("database", () =>
    {
        // Aquí se podría agregar un health check personalizado para la base de datos
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
    })
    .AddCheck("authentication", () =>
    {
        // Health check para el sistema de autenticación
        return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
    });

// =====================================================
// CONFIGURACIÓN DE CACHING
// =====================================================
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000; // Límite de elementos en cache
});

// =====================================================
// CONSTRUCCIÓN DE LA APLICACIÓN
// =====================================================
var app = builder.Build();

// =====================================================
// CONFIGURACIÓN DEL PIPELINE HTTP
// =====================================================

// Swagger solo en desarrollo y staging
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dynamic API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Dynamic API - Documentación";
        c.DefaultModelsExpandDepth(-1); // Colapsar modelos por defecto
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
    });
}

// Health checks
app.MapHealthChecks("/health");

// HTTPS redirection
app.UseHttpsRedirection();

// CORS
app.UseCors("AllowSpecificOrigins");

// Response compression
app.UseResponseCompression();

// =====================================================
// MIDDLEWARE PERSONALIZADO EN ORDEN CORRECTO
// =====================================================

// 1. Request Logging (debe ir primero para capturar todo)
app.UseMiddleware<RequestLoggingMiddleware>();

// 2. Exception Handling (para capturar todas las excepciones)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 3. Authentication (validación de credenciales)
app.UseMiddleware<AuthenticationMiddleware>();

// 4. Rate Limiting (después de autenticación para usar info de credencial)
app.UseRateLimiter();

// 5. Audit (para registrar ejecuciones de APIs)
app.UseMiddleware<AuditMiddleware>();

// 6. Metrics (métricas de performance)
app.UseMiddleware<MetricsMiddleware>();

// =====================================================
// CONFIGURACIÓN DE AUTORIZACIÓN
// =====================================================
app.UseAuthorization();

// =====================================================
// MAPEO DE CONTROLLERS
// =====================================================
app.MapControllers();

// =====================================================
// ENDPOINTS ADICIONALES
// =====================================================

// Endpoint de información de la API
app.MapGet("/api/info", () => new
{
    ApplicationName = "Dynamic API",
    Version = "2.0.0",
    Environment = app.Environment.EnvironmentName,
    AuthenticationTypes = new object[]
    {
        new { Type = "NONE", Description = "Sin autenticación" },
        new { Type = "TOKEN", Description = "Token personalizado", Header = "X-API-Token o Authorization: Bearer" },
        new { Type = "APIKEY", Description = "API Key", Header = "X-API-Key" },
        new { Type = "JWT", Description = "JSON Web Token", Header = "Authorization: Bearer" },
        new { Type = "OAUTH2", Description = "OAuth 2.0", Header = "Authorization: Bearer" },
        new { Type = "BASIC", Description = "Basic Authentication", Header = "Authorization: Basic" },
        new { Type = "NTLM", Description = "NTLM Authentication", Header = "Authorization: NTLM" }
    },
    Timestamp = DateTime.UtcNow
}).WithTags("System");

// Endpoint para obtener APIs disponibles
app.MapGet("/api/available", async (IConfigurationService configService) =>
{
    try
    {
        var apis = await configService.GetAvailableApisAsync();
        return Results.Ok(new
        {
            Success = true,
            Count = apis.Count,
            APIs = apis.Select(api => new
            {
                api.IdAPI,
                api.NombreAPI,
                api.Descripcion,
                api.TipoObjeto,
                api.Endpoint,
                api.ExampleCall,
                ParameterCount = api.Parametros.Count
            })
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error obteniendo APIs disponibles");
        return Results.Problem("Error obteniendo APIs disponibles");
    }
}).WithTags("System");

// =====================================================
// INICIALIZACIÓN Y VALIDACIONES - MEJORADA CON DEBUGGING
// =====================================================

// Validar configuración crítica al inicio
try
{
    Console.WriteLine("=== INICIO VALIDACIÓN DE SERVICIOS ===");

    using var scope = app.Services.CreateScope();
    var dbService = scope.ServiceProvider.GetRequiredService<DatabaseService>();

    Console.WriteLine($"DatabaseService obtenido: {dbService != null}");

    // Verificar configuración del DatabaseService
    if (dbService.IsConfigured())
    {
        var connString = dbService.GetConnectionString();
        Console.WriteLine($"DatabaseService configurado correctamente");
        Console.WriteLine($"Connection string length: {connString?.Length ?? 0}");

        var safeConnString = connString?.Length > 30 ? connString.Substring(0, 30) + "..." : connString;
        Log.Information("Usando connection string: {ConnectionString}", safeConnString);

        // Probar conexión a base de datos
        Console.WriteLine("Probando conexión a base de datos...");
        var connectionTest = await dbService.TestConnectionAsync();
        if (connectionTest)
        {
            Log.Information("✅ Conexión a base de datos validada exitosamente");
            Console.WriteLine("✅ Conexión a base de datos OK");
        }
        else
        {
            Console.WriteLine("❌ Fallo en conexión a base de datos");
            throw new InvalidOperationException("❌ No se pudo conectar a la base de datos");
        }
    }
    else
    {
        Console.WriteLine("❌ DatabaseService NO está configurado");
        throw new InvalidOperationException("❌ DatabaseService no está configurado correctamente");
    }

    // Validar servicios de autenticación
    Console.WriteLine("Validando servicios de autenticación...");
    var tipoAuthService = scope.ServiceProvider.GetRequiredService<ITipoAutenticacionService>();
    var tiposAuth = await tipoAuthService.GetActiveTiposAsync();
    Log.Information("✅ Sistema de autenticación configurado con {Count} tipos disponibles: {Tipos}",
        tiposAuth.Count, string.Join(", ", tiposAuth.Select(t => t.Codigo)));

    Console.WriteLine($"✅ Servicios de autenticación OK - {tiposAuth.Count} tipos disponibles");
    Console.WriteLine("=== FIN VALIDACIÓN DE SERVICIOS ===");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERROR EN VALIDACIÓN: {ex.Message}");
    Console.WriteLine($"StackTrace: {ex.StackTrace}");
    Log.Fatal(ex, "❌ Error crítico durante la inicialización de la aplicación");
    throw;
}

// =====================================================
// LOGGING DE INICIO
// =====================================================
Log.Information("🚀 Dynamic API iniciada exitosamente");
Log.Information("📊 Environment: {Environment}", app.Environment.EnvironmentName);
Log.Information("🔐 Autenticación multi-tipo habilitada");
Log.Information("📈 Rate limiting configurado");
Log.Information("🎯 Swagger disponible en: /swagger (solo desarrollo/staging)");
Log.Information("💚 Health checks disponibles en: /health");

// =====================================================
// EJECUTAR APLICACIÓN
// =====================================================
try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ La aplicación terminó inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}


// =====================================================
// CLASE RequestContext NECESARIA PARA RATE LIMITING
// =====================================================
public class RequestContext
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public string? Credential { get; set; }
    public int? IdAPI { get; set; }
    public int? IdCredencial { get; set; }
    public string? TipoAuth { get; set; } = "NONE";
    public string Environment { get; set; } = "PRODUCTION";
    public string? ClientIP { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}