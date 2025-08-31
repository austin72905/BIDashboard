namespace BIDashboardBackend.Models
{
    public sealed class MaterializedMetric
    {
        public long Id { get; init; }
        public long DatasetId { get; init; }
        public string MetricKey { get; init; } = string.Empty; // age-distribution | gender-share
        public string Bucket { get; init; } = string.Empty; // e.g. 0-9,10-19 or male,female
        public long Value { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
