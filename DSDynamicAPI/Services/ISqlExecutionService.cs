
// =====================================================
// ISqlExecutionService - SIN CAMBIOS MAYORES
// =====================================================
public interface ISqlExecutionService
{
    Task<object?> ExecuteSqlAsync(ApiConfiguration config, Dictionary<string, object?> parameters, string environment);

    // NUEVO: M�todo con contexto de autenticaci�n
    Task<object?> ExecuteSqlWithAuthContextAsync(ApiConfiguration config, Dictionary<string, object?> parameters, string environment, AuthValidationResult authContext);
}