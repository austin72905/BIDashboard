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


        // 匯入資料：MVP 可先以 batched insert；正式可用 COPY
        Task<long> BulkCopyRowsAsync(long batchId, Stream csvStream, CancellationToken ct);


        // 失效快取用
        Task<string[]> GetAffectedMetricKeysAsync(long datasetId);
    }
}
