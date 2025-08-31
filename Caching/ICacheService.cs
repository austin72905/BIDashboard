namespace BIDashboardBackend.Caching
{
    public interface ICacheService
    {
        Task<string?> GetStringAsync(string key);
        Task SetStringAsync(string key, string value, TimeSpan? ttl = null);
        Task RemoveByPrefixAsync(string prefix);
    }
}
