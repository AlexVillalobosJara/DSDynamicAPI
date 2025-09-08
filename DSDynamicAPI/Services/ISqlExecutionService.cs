// Interfaz para ejecución SQL
public interface ISqlExecutionService
{
    Task<object?> ExecuteSqlAsync(ApiConfiguration config, Dictionary<string, object?> parameters, string environment);
}