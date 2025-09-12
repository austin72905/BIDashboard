using BIDashboardBackend.DTOs.Response;

namespace BIDashboardBackend.DTOs.Response
{
    /// <summary>
    /// 所有指標數據的統一 DTO
    /// </summary>
    public sealed class AllMetricsDto
    {
        public long DatasetId { get; init; }
        public KpiSummaryDto KpiSummary { get; init; } = null!;
        public AgeDistributionDto AgeDistribution { get; init; } = null!;
        public GenderShareDto GenderShare { get; init; } = null!;
        public MonthlyRevenueTrendDto MonthlyRevenueTrend { get; init; } = null!;
        public RegionDistributionDto RegionDistribution { get; init; } = null!;
        public ProductCategorySalesDto ProductCategorySales { get; init; } = null!;
        public DateTime UpdatedAt { get; init; }
    }
}
