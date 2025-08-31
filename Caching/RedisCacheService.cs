using StackExchange.Redis;

namespace BIDashboardBackend.Caching
{
    public sealed class RedisCacheService : ICacheService
    {
        private readonly IDatabase _db;
        private readonly IConnectionMultiplexer _mux;


        public RedisCacheService(IConnectionMultiplexer mux)
        {
            _mux = mux;
            _db = mux.GetDatabase();
        }


        public async Task<string?> GetStringAsync(string key)
        => await _db.StringGetAsync(key);


        public async Task SetStringAsync(string key, string value, TimeSpan? ttl = null)
        => await _db.StringSetAsync(key, value, ttl);


        public async Task RemoveByPrefixAsync(string prefix)
        {
            // 注意：Redis 無原生 prefix 刪除，這裡僅示意。實務可用 keyspace scan。
            var endpoints = _mux.GetEndPoints();
            foreach (var ep in endpoints)
            {
                var server = _mux.GetServer(ep);
                var keys = server.Keys(pattern: prefix + "*");
                foreach (var k in keys)
                    await _db.KeyDeleteAsync(k);
            }
        }
    }
}
