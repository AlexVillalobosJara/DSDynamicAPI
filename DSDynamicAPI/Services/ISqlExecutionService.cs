// Interfaz para ejecuci�n SQL
public interface ISqlExecutionService
{
    Task<object?> ExecuteSqlAsync(ApiConfiguration config, Dictionary<string, object?> parameters, string environment);
}