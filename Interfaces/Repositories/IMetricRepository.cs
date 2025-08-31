using BIDashboardBackend.DTOs.Response;

namespace BIDashboardBackend.Interfaces.Repositories
{
    public interface IMetricRepository
    {
        // KPI
        Task<decimal> GetTotalRevenueAsync(long datasetId);
        Task<long> GetTotalCustomersAsync(long datasetId);
        Task<long> GetTotalOrdersAsync(long datasetId);
        Task<decimal> GetAvgOrderValueAsync(long datasetId);
        Task<long> GetNewCustomersAsync(long datasetId, DateOnly since);
        Task<long> GetReturningCustomersAsync(long datasetId, DateOnly since);
        Task<long> GetPendingOrdersAsync(long datasetId);

        // 趨勢 / 分布
        Task<IReadOnlyList<(DateOnly Period, decimal Value)>> GetMonthlyRevenueTrendAsync(long datasetId, int months);
        Task<IReadOnlyList<(string Name, long Value)>> GetRegionDistributionAsync(long datasetId);
        Task<IReadOnlyList<(string Category, long Qty)>> GetProductCategorySalesAsync(long datasetId);
        Task<IReadOnlyList<(string Bucket, long Value)>> GetAgeDistributionAsync(long datasetId);
        Task<IReadOnlyList<(string Gender, long Value)>> GetGenderShareAsync(long datasetId);

        // 寫入/覆蓋物化值（ETL 用）
        Task UpsertMetricAsync(long datasetId, string metricKey, string? bucket, string? bucket2, DateOnly? period, decimal value);
        Task BulkReplaceMetricAsync(long datasetId, string metricKey, IEnumerable<(string? bucket, string? bucket2, DateOnly? period, decimal value)> rows);
    }
}
