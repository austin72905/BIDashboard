using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;

namespace BIDashboardBackend.Interfaces.Repositories
{
    public interface IMetricRepository
    {
        // KPI
        Task<decimal> GetTotalRevenueAsync(long datasetId, long userId);
        Task<long> GetTotalCustomersAsync(long datasetId, long userId);
        Task<long> GetTotalOrdersAsync(long datasetId, long userId);
        Task<decimal> GetAvgOrderValueAsync(long datasetId, long userId);
        Task<long> GetNewCustomersAsync(long datasetId, DateTime since, long userId);
        Task<long> GetReturningCustomersAsync(long datasetId, DateTime since, long userId);
        Task<long> GetPendingOrdersAsync(long datasetId, long userId);

        // 趨勢 / 分布
        Task<IReadOnlyList<(DateTime Period, decimal Value)>> GetMonthlyRevenueTrendAsync(long datasetId, int months, long userId);
        Task<IReadOnlyList<(string Name, long Value)>> GetRegionDistributionAsync(long datasetId, long userId);
        Task<IReadOnlyList<(string Category, long Qty)>> GetProductCategorySalesAsync(long datasetId, long userId);
        Task<IReadOnlyList<(string Bucket, long Value)>> GetAgeDistributionAsync(long datasetId, long userId);
        Task<IReadOnlyList<(string Gender, long Value)>> GetGenderShareAsync(long datasetId, long userId);

        // 寫入/覆蓋物化值（ETL 用）
        Task UpsertMetricAsync(long datasetId, MetricKey key, string bucket, decimal value);
        Task BulkReplaceMetricAsync(long datasetId, MetricKey key, IEnumerable<(string bucket, decimal value)> rows);
    }
}