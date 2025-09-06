using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Models;

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
        /// <summary>
        /// 單次查詢回所有儀表板所需的度量資料（已合併 SQL，一次往返）。
        /// 以通用列格式回傳，Service 端再組裝成 AllMetricsDto。
        /// </summary>
        Task<IReadOnlyList<MetricRow>> GetAllMetricsRowsAsync(
            long datasetId,
            long userId,
            int months = 12);
    }
}