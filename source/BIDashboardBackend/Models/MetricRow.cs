namespace BIDashboardBackend.Models
{
    // 對應 SQL 的單一結果列
    public sealed class MetricRow
    {
        public string Metric { get; init; } = default!;
        public string? Bucket { get; init; }
        public DateTime? Period { get; init; }
        public decimal Value { get; init; }
    }

}
