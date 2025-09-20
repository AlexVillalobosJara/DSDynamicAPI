namespace DSDynamicAPI.Services
{
    public interface IDatabaseService
    {
        public Task<int> QuerySingleIntAsync(string sql, object? param = null, int? commandTimeout = null);
        public Task<long> QuerySingleLongAsync(string sql, object? param = null, int? commandTimeout = null);
    }
}
