namespace BIDashboardBackend.DTOs.Response
{
    // 趨勢資料點
    public sealed class TrendPointDto
    {
        public DateTime Period { get; init; }
        public decimal Value { get; init; }
    }

    // 月營收趨勢回應
    public sealed class MonthlyRevenueTrendDto
    {
        public long DatasetId { get; init; }
        public IReadOnlyList<TrendPointDto> Points { get; init; } = new List<TrendPointDto>();
        public string Unit { get; init; } = "currency";
    }
}