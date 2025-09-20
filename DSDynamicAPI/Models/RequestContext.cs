// Contexto de request para middleware (ACTUALIZADO)
//public class RequestContext
//{
//    public string RequestId { get; set; } = Guid.NewGuid().ToString();
//    public DateTime StartTime { get; set; } = DateTime.UtcNow;
//    public string? Credential { get; set; } // Reemplaza Token
//    public int? IdAPI { get; set; }
//    public int? IdCredencial { get; set; } // Reemplaza IdToken
//    public string? TipoAuth { get; set; } = "NONE"; // Nuevo campo
//    public string Environment { get; set; } = "PRODUCTION";
//    public string? ClientIP { get; set; }
//    public Dictionary<string, object> Metadata { get; set; } = new();
//}