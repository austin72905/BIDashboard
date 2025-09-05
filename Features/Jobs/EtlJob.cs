using BIDashboardBackend.Caching;
using BIDashboardBackend.Interfaces;
using Hangfire;

namespace BIDashboardBackend.Features.Jobs
{
    public sealed class EtlJob : IEtlJob
    {
        private readonly IUnitOfWork _uow;
        private readonly ISqlRunner _sql;
        private readonly CacheKeyBuilder _keyBuilder;
        private readonly ICacheService _cache;


        public EtlJob(IUnitOfWork uow, ISqlRunner sql, CacheKeyBuilder keyBuilder, ICacheService cache)
        {
            _sql = sql;
            _uow = uow;
            _keyBuilder = keyBuilder;
            _cache = cache;
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task ProcessBatch(long datasetId, long batchId)
        {
            await _uow.BeginAsync();

            // A) by-batch：先刪舊 → 依 mapping 重算寫回
            await RebuildByBatchAsync(datasetId, batchId);

            // B) final：只重算「本批影響的切片」並覆寫最終表
            await UpsertFinalForAffectedAsync(datasetId, batchId);

            // C) 標記成功
            await _sql.ExecAsync(
                "UPDATE dataset_batches SET status='Succeeded', updated_at=NOW() WHERE id=@BatchId",
                new { BatchId = batchId });

            await _uow.CommitAsync();

            // D) 清快取（只清受影響 metric 的前綴）
            //var metricKeys = await _sql.QueryAsync<int>(@"
            //    SELECT DISTINCT metric_key
            //    FROM materialized_metrics_by_batch
            //    WHERE dataset_id=@DatasetId AND batch_id=@BatchId",
            //    new { DatasetId = datasetId, BatchId = batchId });

            //foreach (var mk in metricKeys)
            //    await _cache.RemoveByPrefixAsync($"metric:{datasetId}:{mk}:");
        }

        // === 依你的欄位命名改這兩個方法即可 ===
        private async Task RebuildByBatchAsync(long datasetId, long batchId)
        {
            await _sql.ExecAsync(SqlByBatch, new { DatasetId = datasetId, BatchId = batchId });
        }

        
        private async Task UpsertFinalForAffectedAsync(long datasetId, long batchId)
        {
            await _sql.ExecAsync(SqlUpsertFinalForAffected, new { DatasetId = datasetId, BatchId = batchId });
        }


        // 清舊 → 依 mapping 解析 rowjson → INSERT by-batch 統計值
        private const string SqlByBatch = @"CALL sp_mm_by_batch(@DatasetId, @BatchId);";

        // 「找出受影響切片 → 跨所有批次聚合 → UPSERT 到 final 表 materialized_metrics」
        private const string SqlUpsertFinalForAffected = @"CALL sp_mm_upsert_final_for_affected(@DatasetId, @BatchId);";

    }
}
