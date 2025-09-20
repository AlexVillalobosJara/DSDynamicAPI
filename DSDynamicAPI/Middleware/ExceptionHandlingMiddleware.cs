// =====================================================
// ExceptionHandlingMiddleware - ACTUALIZADO
// =====================================================
using Microsoft.Data.SqlClient;
using System.Text.Json;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var requestContext = context.Items["RequestContext"] as RequestContext;
        var requestId = requestContext?.RequestId ?? Guid.NewGuid().ToString();

        // Log con información de contexto de autenticación
        var logMessage = "Excepción no manejada en request {RequestId}: {Message}";
        var logParams = new object[] { requestId, exception.Message };

        if (requestContext?.IdAPI.HasValue == true)
        {
            logMessage += " - API: {IdAPI}";
            logParams = logParams.Append(requestContext.IdAPI.Value).ToArray();

            if (!string.IsNullOrEmpty(requestContext.TipoAuth))
            {
                logMessage += " - Auth: {TipoAuth}";
                logParams = logParams.Append(requestContext.TipoAuth).ToArray();
            }
        }

        _logger.LogError(exception, logMessage, logParams);

        var statusCode = GetStatusCode(exception);
        var message = GetErrorMessage(exception);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new ErrorResponse
        {
            Error = GetErrorType(exception),
            Message = message,
            StatusCode = statusCode,
            RequestId = requestId,
            Timestamp = DateTime.UtcNow
        };

        // En desarrollo, incluir stack trace
        if (_environment.IsDevelopment())
        {
            errorResponse.Message += $"\n\nStack Trace:\n{exception.StackTrace}";
        }

        var json = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private int GetStatusCode(Exception exception)
    {
        return exception switch
        {
            ArgumentException => 400,
            InvalidOperationException => 400,
            UnauthorizedAccessException => 401,
            NotSupportedException => 501,
            TimeoutException => 408,
            SqlException => 500,
            _ => 500
        };
    }

    private string GetErrorMessage(Exception exception)
    {
        return exception switch
        {
            ArgumentException => exception.Message,
            InvalidOperationException => exception.Message,
            UnauthorizedAccessException => "Acceso no autorizado",
            NotSupportedException => "Operación no soportada",
            TimeoutException => "Timeout en la operación",
            SqlException => "Error de base de datos",
            _ => _environment.IsDevelopment() ? exception.Message : "Error interno del servidor"
        };
    }

    private string GetErrorType(Exception exception)
    {
        return exception switch
        {
            ArgumentException => "INVALID_ARGUMENT",
            InvalidOperationException => "INVALID_OPERATION",
            UnauthorizedAccessException => "UNAUTHORIZED",
            NotSupportedException => "NOT_SUPPORTED",
            TimeoutException => "TIMEOUT",
            SqlException => "DATABASE_ERROR",
            _ => "INTERNAL_ERROR"
        };
    }
}