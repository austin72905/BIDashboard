namespace BIDashboardBackend.Configs
{
    public sealed class RedisOptions
    {
        public string? ConnectionString { get; init; }
        public int DefaultTtlMinutes { get; init; } = 10;
        public string KeyPrefix { get; init; } = "bi";
    }
}
