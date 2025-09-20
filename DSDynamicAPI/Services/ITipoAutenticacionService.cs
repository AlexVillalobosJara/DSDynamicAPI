// Services/Interfaces/ITipoAutenticacionService.cs

namespace DynamicAPIs.Services.Interfaces;

public interface ITipoAutenticacionService
{
    // =====================================================
    // GESTIÓN DE TIPOS DE AUTENTICACIÓN
    // =====================================================

    /// <summary>
    /// Obtiene todos los tipos de autenticación disponibles
    /// </summary>
    Task<List<TipoAutenticacionDto>> GetAllTiposAsync();

    /// <summary>
    /// Obtiene solo los tipos de autenticación activos
    /// </summary>
    Task<List<TipoAutenticacionDto>> GetActiveTiposAsync();

    /// <summary>
    /// Obtiene un tipo de autenticación por su ID
    /// </summary>
    Task<TipoAutenticacionDto?> GetTipoByIdAsync(int idTipoAuth);

    /// <summary>
    /// Obtiene un tipo de autenticación por su código
    /// </summary>
    Task<TipoAutenticacionDto?> GetTipoByCodigoAsync(string codigo);

    // =====================================================
    // VALIDACIÓN DE CONFIGURACIONES
    // =====================================================

    /// <summary>
    /// Valida la configuración JSON para un tipo de autenticación específico
    /// </summary>
    Task<bool> ValidateConfigurationAsync(TipoAutenticacion tipo, string? configuracion);

    /// <summary>
    /// Obtiene la configuración por defecto para un tipo de autenticación
    /// </summary>
    Task<string?> GetDefaultConfigurationAsync(TipoAutenticacion tipo);

    // =====================================================
    // MÉTODOS DE UTILIDAD
    // =====================================================

    /// <summary>
    /// Obtiene todas las configuraciones por defecto para tipos que las requieren
    /// </summary>
    Task<Dictionary<string, string>> GetAllDefaultConfigurationsAsync();

    /// <summary>
    /// Verifica si un código de tipo de autenticación es válido y está activo
    /// </summary>
    Task<bool> IsValidAuthTypeAsync(string codigo);

    /// <summary>
    /// Obtiene la lista de campos requeridos para la configuración de un tipo
    /// </summary>
    Task<List<string>> GetRequiredConfigurationFieldsAsync(TipoAutenticacion tipo);

    /// <summary>
    /// Obtiene el JSON Schema para validación en frontend
    /// </summary>
    Task<string?> GetConfigurationSchemaAsync(TipoAutenticacion tipo);
}