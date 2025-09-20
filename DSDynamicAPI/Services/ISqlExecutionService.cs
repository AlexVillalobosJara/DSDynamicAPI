
// =====================================================
// ISqlExecutionService - SIN CAMBIOS MAYORES
// =====================================================
public interface ISqlExecutionService
{
    Task<object?> ExecuteSqlAsync(ApiConfiguration config, Dictionary<string, object?> parameters, string environment);

    // NUEVO: Método con contexto de autenticación
    Task<object?> ExecuteSqlWithAuthContextAsync(ApiConfiguration config, Dictionary<string, object?> parameters, string environment, AuthValidationResult authContext);
}