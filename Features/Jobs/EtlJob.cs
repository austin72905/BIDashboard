using BIDashboardBackend.Caching;
using BIDashboardBackend.Interfaces;
using Hangfire;

namespace BIDashboardBackend.Features.Jobs
{
    /// <summary>
    /// 負責執行資料集批次 ETL 的 Hangfire 工作
    /// </summary>
    public sealed class EtlJob : IEtlJob
    {
        // 交易與 SQL 執行相關的服務
        private readonly IUnitOfWork _uow;
        private readonly ISqlRunner _sql;

        // 清除快取所需的工具與服務
        private readonly CacheKeyBuilder _keyBuilder;
        private readonly ICacheService _cache;

        /// <summary>
        /// 建構子：注入資料庫與快取相關服務
        /// </summary>
        public EtlJob(IUnitOfWork uow, ISqlRunner sql, CacheKeyBuilder keyBuilder, ICacheService cache)
        {
            _sql = sql;
            _uow = uow;
            _keyBuilder = keyBuilder;
            _cache = cache;
        }

        /// <summary>
        /// 處理單一批次的 ETL 流程
        /// </summary>
        /// <param name="datasetId">資料集識別碼</param>
        /// <param name="batchId">批次識別碼，-1表示重新計算整個資料集</param>
        [AutomaticRetry(Attempts = 3)]
        public async Task ProcessBatch(long datasetId, long batchId)
        {
            // 以交易包住整個流程，確保要嘛全部成功、要嘛全部回滾
            await _uow.BeginAsync();

            if (batchId == -1)
            {
                // 重新計算整個資料集
                //await RebuildEntireDatasetAsync(datasetId);
                await UpsertFinalForAffectedAsync(datasetId, batchId);
            }
            else
            {
                // A) by-batch：刪除舊資料並依映射重新計算
                await RebuildByBatchAsync(datasetId, batchId);

                // B) final：重新計算整個 dataset 的所有指標
                await UpsertFinalForAffectedAsync(datasetId, batchId);

                // C) 標記批次處理成功
                await _sql.ExecAsync(
                    "UPDATE dataset_batches SET status='Succeeded', updated_at=NOW() WHERE id=@BatchId",
                    new { BatchId = batchId });
            }

            // D) 提交交易，寫入以上所有變更
            await _uow.CommitAsync();

            // E) 清快取：清除該資料集相關的指標快取
            var cachePrefix = _keyBuilder.MetricPrefix(datasetId);
            await _cache.RemoveByPrefixAsync(cachePrefix);
        }

        /// <summary>
        /// 重新計算整個資料集的統計資料
        /// </summary>
        private async Task RebuildEntireDatasetAsync(long datasetId)
        {
            // 1. 清除該資料集的所有舊統計資料
            await ClearAllMetricsForDatasetAsync(datasetId);

            // 2. 重新計算所有剩餘批次的統計資料
            await RecalculateAllBatchesAsync(datasetId);

            // 3. 重新聚合最終統計資料
            await RebuildFinalMetricsAsync(datasetId);
        }

        /// <summary>
        /// 重新建立批次統計資料：先刪除舊資料再寫入新統計值
        /// </summary>
        private async Task RebuildByBatchAsync(long datasetId, long batchId)
        {
            // 先刪除該批次既有的統計值
            await ClearOldByBatchAsync(datasetId, batchId);

            // 再依據欄位映射重新寫入統計值
            await InsertByBatchAsync(datasetId, batchId);
        }

        /// <summary>
        /// 重新計算整個 dataset 的所有指標
        /// </summary>
        private async Task UpsertFinalForAffectedAsync(long datasetId, long batchId)
        {
            // 重新計算整個 dataset 的所有指標並寫回最終表
            await UpsertFinalMetricsAsync(datasetId, batchId);
        }

        /// <summary>
        /// 清除指定批次既有的 by-batch 統計資料
        /// </summary>
        private async Task ClearOldByBatchAsync(long datasetId, long batchId)
        {
            // 呼叫 fn_mm_clear_old_by_batch 刪除舊的 by-batch 統計值
            await _sql.ExecAsync(SqlClearOldByBatch, new { DatasetId = datasetId, BatchId = batchId });
        }

        /// <summary>
        /// 依映射解析資料列並寫入新的 by-batch 統計值
        /// </summary>
        private async Task InsertByBatchAsync(long datasetId, long batchId)
        {
            // 呼叫 fn_mm_insert_metrics_by_batch 解析並寫入統計值
            await _sql.ExecAsync(SqlInsertByBatch, new { DatasetId = datasetId, BatchId = batchId });
        }

        /// <summary>
        /// 重新彙總受影響的切片並寫入最終統計表
        /// </summary>
        private async Task UpsertFinalMetricsAsync(long datasetId, long batchId)
        {
            // 呼叫 fn_mm_upsert_final_metrics 將 by-batch 統計整合到最終表
            await _sql.ExecAsync(SqlUpsertFinalMetrics, new { DatasetId = datasetId, BatchId = batchId });
        }

        // 以下 SQL 常數對應資料庫中的函式呼叫

        /// <summary>
        /// 刪除舊的 by-batch 統計值
        /// </summary>
        private const string SqlClearOldByBatch = @"SELECT fn_mm_clear_old_by_batch(@DatasetId, @BatchId);";

        /// <summary>
        /// 計算並寫入新的 by-batch 統計值
        /// </summary>
        private const string SqlInsertByBatch = @"SELECT fn_mm_insert_metrics_by_batch(@DatasetId, @BatchId);";

        /// <summary>
        /// 重新彙總並更新最終統計表
        /// </summary>
        private const string SqlUpsertFinalMetrics = @"SELECT fn_mm_upsert_final_metrics(@DatasetId, @BatchId);";

        /// <summary>
        /// 清除指定資料集的所有統計資料
        /// </summary>
        private async Task ClearAllMetricsForDatasetAsync(long datasetId)
        {
            // 清除該資料集的所有 by-batch 統計資料
            await _sql.ExecAsync("DELETE FROM materialized_metrics_by_batch WHERE dataset_id = @DatasetId", new { DatasetId = datasetId });
            
            // 清除該資料集的所有最終統計資料
            await _sql.ExecAsync("DELETE FROM materialized_metrics WHERE dataset_id = @DatasetId", new { DatasetId = datasetId });
        }

        /// <summary>
        /// 重新計算指定資料集的所有剩餘批次
        /// </summary>
        private async Task RecalculateAllBatchesAsync(long datasetId)
        {
            // 獲取該資料集的所有剩餘批次
            const string getBatchesSql = @"
                SELECT id FROM dataset_batches 
                WHERE dataset_id = @DatasetId AND status = 'Succeeded'
                ORDER BY id;";
            
            var batchIds = await _sql.QueryAsync<long>(getBatchesSql, new { DatasetId = datasetId });
            
            // 重新計算每個批次的統計資料
            foreach (var batchId in batchIds)
            {
                await InsertByBatchAsync(datasetId, batchId);
            }
        }

        /// <summary>
        /// 重新建立最終統計資料
        /// </summary>
        private async Task RebuildFinalMetricsAsync(long datasetId)
        {
            // 重新聚合所有 by-batch 統計資料到最終表
            const string rebuildFinalSql = @"
                INSERT INTO materialized_metrics (dataset_id, metric_key, bucket, period, value, updated_at)
                SELECT 
                    @datasetId as dataset_id,
                    metric_key,
                    bucket,
                    period,
                    -- 根據指標類型選擇適當的聚合值
                    CASE 
                        -- 營收相關指標 (TotalRevenue, ProductCategorySales, MonthlyRevenueTrend)
                        WHEN metric_key IN (0, 8, 9) THEN SUM(sum_value)
                        -- 計數相關指標 (TotalCustomers, TotalOrders, PendingOrders, RegionDistribution, AgeDistribution, GenderShare)
                        WHEN metric_key IN (1, 2, 6, 10, 11, 12) THEN SUM(count_value)
                        -- 平均訂單金額 (AvgOrderValue) - 需要特殊處理
                        WHEN metric_key = 3 THEN 
                            CASE 
                                WHEN SUM(count_value) > 0 THEN SUM(sum_value) / SUM(count_value)
                                ELSE 0 
                            END
                        -- 預設情況
                        ELSE COALESCE(SUM(sum_value), SUM(count_value), 0)
                    END as value,
                    NOW() as updated_at
                FROM materialized_metrics_by_batch
                WHERE dataset_id = @datasetId
                GROUP BY metric_key, bucket, period;";
            
            await _sql.ExecAsync(rebuildFinalSql, new { datasetId });
        }
    }
}
