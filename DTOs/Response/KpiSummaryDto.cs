namespace BIDashboardBackend.DTOs.Response
{
    // 一次回傳：總營收、總客戶、總訂單、平均客單、新客、回購、處理中
    public sealed class KpiSummaryDto
    {
        public long DatasetId { get; init; }
        public decimal TotalRevenue { get; init; }
        public long TotalCustomers { get; init; }
        public long TotalOrders { get; init; }
        public decimal AvgOrderValue { get; init; }
        public long NewCustomers { get; init; }
        public long ReturningCustomers { get; init; }
        public long PendingOrders { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}