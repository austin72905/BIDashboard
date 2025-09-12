using BIDashboardBackend.Configs;
using Microsoft.Extensions.Options;

namespace BIDashboardBackend.Caching
{
    public sealed class CacheKeyBuilder
    {
        private readonly string _prefix;
        public CacheKeyBuilder(IOptions<RedisOptions> opt) => _prefix = opt.Value.KeyPrefix ?? "bi";

        public string Metric(long datasetId, string metric, string bucket)
            => $"{_prefix}:{datasetId}:metric:{metric}:{bucket}";

        public string MetricKey(long datasetId, string metricKey)
            => $"{_prefix}:{datasetId}:metric:{metricKey}";

        public string MetricPrefix(long datasetId)
            => $"{_prefix}:{datasetId}:metric:";
    }
}