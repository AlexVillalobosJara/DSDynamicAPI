// Servicio de validación de tokens
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

public class TokenValidationService : ITokenValidationService
{
    private readonly DatabaseOptions _dbOptions;
    private readonly ILogger<TokenValidationService> _logger;

    public TokenValidationService(IOptions<DatabaseOptions> dbOptions, ILogger<TokenValidationService> logger)
    {
        _dbOptions = dbOptions.Value;
        _logger = logger;
    }

    public async Task<TokenValidationResult> ValidateTokenAsync(string token)
    {
        try
        {
            using var connection = new SqlConnection(_dbOptions.ConfigConnectionString);

            var parameters = new DynamicParameters();
            parameters.Add("@TokenValue", token);
            parameters.Add("@IdAPI", dbType: DbType.Int32, direction: ParameterDirection.Output);
            parameters.Add("@IsValid", dbType: DbType.Boolean, direction: ParameterDirection.Output);
            parameters.Add("@RateLimitExceeded", dbType: DbType.Boolean, direction: ParameterDirection.Output);

            await connection.ExecuteAsync("sp_ValidateTokenAndRateLimit", parameters,
                commandType: CommandType.StoredProcedure, commandTimeout: _dbOptions.DefaultCommandTimeout);

            return new TokenValidationResult
            {
                IdAPI = parameters.Get<int>("@IdAPI"),
                IsValid = parameters.Get<bool>("@IsValid"),
                RateLimitExceeded = parameters.Get<bool>("@RateLimitExceeded"),
                ErrorMessage = parameters.Get<bool>("@RateLimitExceeded") ? "Rate limit exceeded" : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validando token");
            return new TokenValidationResult
            {
                IsValid = false,
                ErrorMessage = "Error interno validando token"
            };
        }
    }
}