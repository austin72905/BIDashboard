namespace BIDashboardBackend.Interfaces
{
    public interface ISqlRunner
    {
        Task<int> ExecAsync(string sql, object? args = null, int? timeoutSec = null);
        Task<T?> ScalarAsync<T>(string sql, object? args = null, int? timeoutSec = null);
        Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? args = null, int? timeoutSec = null);
        Task<T?> FirstOrDefaultAsync<T>(string sql, object? args = null, int? timeoutSec = null);
    }
}
