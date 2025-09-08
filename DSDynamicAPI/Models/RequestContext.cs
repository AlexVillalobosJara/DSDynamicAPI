// Contexto de request para middleware
public class RequestContext
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public string? Token { get; set; }
    public int? IdAPI { get; set; }
    public int? IdToken { get; set; }
    public string Environment { get; set; } = "PRODUCTION";
    public string? ClientIP { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}