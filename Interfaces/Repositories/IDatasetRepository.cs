using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Models;

namespace BIDashboardBackend.Interfaces.Repositories
{
    public interface IDatasetRepository
    {
        Task<long> CreateDatasetAsync(string name, long? ownerId = null);
        Task<long> CreateBatchAsync(long datasetId, string fileName, long totalRows);
        Task<int> SetBatchStatusAsync(long batchId, string status, string? errorMessage);
        Task<int> UpsertColumnsAsync(long batchId, IEnumerable<DatasetColumn> columns);
        Task<int> UpsertMappingsAsync(long batchId, IEnumerable<DatasetMapping> mappings);
        Task<IReadOnlyList<DatasetColumn>> GetColumnsAsync(long batchId);
        Task<HashSet<string>> GetAvailableSourceColumnsAsync(long batchId);
        Task<DatasetBatch?> GetBatchAsync(long batchId);

        /// <summary>
        /// 使用 JOIN 查詢取得欄位及其映射資訊
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <returns>包含映射資訊的欄位列表</returns>
        Task<IReadOnlyList<DatasetColumnWithMapping>> GetColumnsWithMappingAsync(long batchId);

        // 匯入資料：MVP 可先以 batched insert；正式可用 COPY
        Task<long> BulkCopyRowsAsync(long batchId, Stream csvStream, CancellationToken ct);


        // 失效快取用
        Task<string[]> GetAffectedMetricKeysAsync(long datasetId);

        /// <summary>
        /// 獲取用戶的上傳歷史紀錄（批次列表）
        /// </summary>
        /// <param name="userId">用戶 ID</param>
        /// <param name="limit">限制筆數，預設 50</param>
        /// <param name="offset">偏移量，預設 0</param>
        /// <returns>上傳歷史紀錄列表</returns>
        Task<IReadOnlyList<UploadHistoryDto>> GetUploadHistoryAsync(long userId, int limit = 50, int offset = 0);

        /// <summary>
        /// 獲取指定批次的詳細資訊（包含欄位和映射）
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <param name="userId">用戶 ID（用於權限驗證）</param>
        /// <returns>批次詳細資訊</returns>
        Task<UploadHistoryDto?> GetBatchDetailsAsync(long batchId, long userId);
    }
}
