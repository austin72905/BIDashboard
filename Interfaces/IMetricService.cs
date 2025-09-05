using BIDashboardBackend.DTOs.Response;

namespace BIDashboardBackend.Interfaces
{
    public interface IMetricService
    {
        /// <summary>
        /// 獲取 KPI 摘要（總營收、總客戶、總訂單等）
        /// </summary>
        Task<KpiSummaryDto> GetKpiSummaryAsync(long datasetId,long userId);

        /// <summary>
        /// 獲取年齡分布統計
        /// </summary>
        Task<AgeDistributionDto> GetAgeDistributionAsync(long datasetId, long userId);

        /// <summary>
        /// 獲取性別比例
        /// </summary>
        Task<GenderShareDto> GetGenderShareAsync(long datasetId, long userId);

        /// <summary>
        /// 獲取月營收趨勢
        /// </summary>
        Task<MonthlyRevenueTrendDto> GetMonthlyRevenueTrendAsync(long datasetId, long userId, int months = 12);

        /// <summary>
        /// 獲取地區分布
        /// </summary>
        Task<RegionDistributionDto> GetRegionDistributionAsync(long datasetId, long userId);

        /// <summary>
        /// 獲取產品類別銷量
        /// </summary>
        Task<ProductCategorySalesDto> GetProductCategorySalesAsync(long datasetId, long userId);

        /// <summary>
        /// 清除指定資料集的快取
        /// </summary>
        Task RemoveDatasetCacheAsync(long datasetId);
    }
}