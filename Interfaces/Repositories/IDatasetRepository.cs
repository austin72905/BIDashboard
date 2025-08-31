using BIDashboardBackend.Models;
using System.Data;

namespace BIDashboardBackend.Interfaces.Repositories
{
    public interface IDatasetRepository
    {
        Task<long> CreateBatchAsync(string fileName, long totalRows, IDbTransaction tx);
        Task<int> SetBatchStatusAsync(long batchId, string status, string? errorMessage, IDbTransaction tx);
        Task<int> UpsertColumnsAsync(long batchId, IEnumerable<DatasetColumn> columns, IDbTransaction tx);
        Task<int> UpsertMappingsAsync(long batchId, IEnumerable<DatasetMapping> mappings, IDbTransaction tx);


        // 匯入資料：MVP 可先以 batched insert；正式可用 COPY
        Task<long> BulkCopyRowsAsync(long batchId, Stream csvStream, CancellationToken ct);


        // 失效快取用
        Task<string[]> GetAffectedMetricKeysAsync(long datasetId);
    }
}
