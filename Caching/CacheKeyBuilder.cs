namespace BIDashboardBackend.Caching
{
    public sealed class CacheKeyBuilder
    {
        private readonly string _prefix;
        public CacheKeyBuilder(string prefix) => _prefix = prefix;


        public string Metric(long datasetId, string metric, string bucket)
        => $"{_prefix}:{datasetId}:metric:{metric}:{bucket}";


        public string MetricPrefix(long datasetId)
        => $"{_prefix}:{datasetId}:metric:";
    }
}
