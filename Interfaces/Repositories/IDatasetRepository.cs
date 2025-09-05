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
    }
}
