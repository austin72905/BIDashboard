namespace BIDashboardBackend.Interfaces.Repositories
{
    /// <summary>
    /// ETL 資料處理的 Repository 介面
    /// 封裝所有 ETL 相關的資料庫操作，便於未來抽換資料庫
    /// </summary>
    public interface IEtlRepository
    {
        /// <summary>
        /// 清除指定批次既有的 by-batch 統計資料
        /// </summary>
        /// <param name="datasetId">資料集 ID</param>
        /// <param name="batchId">批次 ID</param>
        /// <returns>受影響的行數</returns>
        Task<int> ClearOldByBatchAsync(long datasetId, long batchId);

        /// <summary>
        /// 依映射解析資料列並寫入新的 by-batch 統計值
        /// </summary>
        /// <param name="datasetId">資料集 ID</param>
        /// <param name="batchId">批次 ID</param>
        /// <returns>受影響的行數</returns>
        Task<int> InsertByBatchAsync(long datasetId, long batchId);

        /// <summary>
        /// 重新彙總受影響的切片並寫入最終統計表
        /// </summary>
        /// <param name="datasetId">資料集 ID</param>
        /// <param name="batchId">批次 ID</param>
        /// <returns>受影響的行數</returns>
        Task<int> UpsertFinalMetricsAsync(long datasetId, long batchId);

        /// <summary>
        /// 清除指定資料集的所有統計資料
        /// </summary>
        /// <param name="datasetId">資料集 ID</param>
        /// <returns>Task</returns>
        Task ClearAllMetricsForDatasetAsync(long datasetId);

        /// <summary>
        /// 重新計算指定資料集的所有剩餘批次
        /// </summary>
        /// <param name="datasetId">資料集 ID</param>
        /// <returns>Task</returns>
        Task RecalculateAllBatchesAsync(long datasetId);

        /// <summary>
        /// 重新建立最終統計資料
        /// </summary>
        /// <param name="datasetId">資料集 ID</param>
        /// <returns>Task</returns>
        Task RebuildFinalMetricsAsync(long datasetId);

        /// <summary>
        /// 更新批次狀態為成功
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <returns>受影響的行數</returns>
        Task<int> UpdateBatchStatusToSucceededAsync(long batchId);

        /// <summary>
        /// 獲取指定資料集的所有成功批次 ID
        /// </summary>
        /// <param name="datasetId">資料集 ID</param>
        /// <returns>批次 ID 列表</returns>
        Task<IReadOnlyList<long>> GetSuccessfulBatchIdsAsync(long datasetId);
    }
}

