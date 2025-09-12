namespace BIDashboardBackend.Configs
{
    public sealed class HangfireOptions
    {
        public string? StorageConnectionString { get; init; }
        public string[] Queues { get; init; } = new[] { "default" };
    }
}
