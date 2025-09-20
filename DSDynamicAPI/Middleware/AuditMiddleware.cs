// =====================================================
// AuditMiddleware - NUEVO (para auditoría automática)
// =====================================================
using System.Diagnostics;
using System.Text.Json;

public class AuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditMiddleware> _logger;

    public AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService auditService)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestContext = context.Items["RequestContext"] as RequestContext;

        if (requestContext?.IdAPI == null)
        {
            await _next(context);
            return;
        }

        var parametrosEnviados = ExtractParameters(context);
        var ambiente = requestContext.Environment;
        var direccionIP = requestContext.ClientIP;

        bool esExitoso = false;
        string? mensajeError = null;

        try
        {
            await _next(context);

            // Considerar exitoso si el status code es 2xx
            esExitoso = context.Response.StatusCode >= 200 && context.Response.StatusCode < 300;

            if (!esExitoso && context.Response.StatusCode != 404) // 404 se maneja aparte
            {
                mensajeError = $"HTTP {context.Response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            esExitoso = false;
            mensajeError = ex.Message;
            throw; // Re-lanzar para que el ExceptionHandlingMiddleware lo maneje
        }
        finally
        {
            stopwatch.Stop();

            // Registrar auditoría de forma asíncrona
            _ = Task.Run(async () =>
            {
                try
                {
                    await auditService.LogAuditoriaAsync(
                        requestContext.IdAPI.Value,
                        requestContext.IdCredencial,
                        ambiente,
                        parametrosEnviados,
                        esExitoso,
                        mensajeError,
                        (int)stopwatch.ElapsedMilliseconds,
                        direccionIP);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error registrando auditoría para API {IdAPI}", requestContext.IdAPI);
                }
            });
        }
    }

    private string? ExtractParameters(HttpContext context)
    {
        try
        {
            var parameters = new Dictionary<string, object>();

            // Query parameters
            foreach (var query in context.Request.Query)
            {
                if (query.Key != "idApi" && query.Key != "environment") // Excluir parámetros del sistema
                {
                    parameters[query.Key] = query.Value.ToString();
                }
            }

            // Form parameters (si es POST)
            if (context.Request.HasFormContentType && context.Request.Form.Any())
            {
                foreach (var form in context.Request.Form)
                {
                    parameters[form.Key] = form.Value.ToString();
                }
            }

            return parameters.Any() ? JsonSerializer.Serialize(parameters) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extrayendo parámetros para auditoría");
            return null;
        }
    }
}