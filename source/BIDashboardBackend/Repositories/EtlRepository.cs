using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;

namespace BIDashboardBackend.Repositories
{
    /// <summary>
    /// ETL 資料處理的 Repository 實作
    /// 封裝所有 ETL 相關的資料庫操作，使用 PostgreSQL 作為後端
    /// </summary>
    public sealed class EtlRepository : IEtlRepository
    {
        private readonly ISqlRunner _sql;

        public EtlRepository(ISqlRunner sql) => _sql = sql;

        /// <summary>
        /// 清除指定批次既有的 by-batch 統計資料
        /// </summary>
        public async Task<int> ClearOldByBatchAsync(long datasetId, long batchId)
        {
            const string sql = @"SELECT fn_mm_clear_old_by_batch(@DatasetId, @BatchId);";
            return await _sql.ExecAsync(sql, new { DatasetId = datasetId, BatchId = batchId });
        }

        /// <summary>
        /// 依映射解析資料列並寫入新的 by-batch 統計值
        /// </summary>
        public async Task<int> InsertByBatchAsync(long datasetId, long batchId)
        {
            const string sql = @"SELECT fn_mm_insert_metrics_by_batch(@DatasetId, @BatchId);";
            return await _sql.ExecAsync(sql, new { DatasetId = datasetId, BatchId = batchId });
        }

        /// <summary>
        /// 重新彙總受影響的切片並寫入最終統計表
        /// </summary>
        public async Task<int> UpsertFinalMetricsAsync(long datasetId, long batchId)
        {
            const string sql = @"SELECT fn_mm_upsert_final_metrics(@DatasetId, @BatchId);";
            return await _sql.ExecAsync(sql, new { DatasetId = datasetId, BatchId = batchId });
        }

        /// <summary>
        /// 清除指定資料集的所有統計資料
        /// </summary>
        public async Task ClearAllMetricsForDatasetAsync(long datasetId)
        {
            // 清除該資料集的所有 by-batch 統計資料
            await _sql.ExecAsync(
                "DELETE FROM materialized_metrics_by_batch WHERE dataset_id = @DatasetId", 
                new { DatasetId = datasetId });
            
            // 清除該資料集的所有最終統計資料
            await _sql.ExecAsync(
                "DELETE FROM materialized_metrics WHERE dataset_id = @DatasetId", 
                new { DatasetId = datasetId });
        }

        /// <summary>
        /// 重新計算指定資料集的所有剩餘批次
        /// </summary>
        public async Task RecalculateAllBatchesAsync(long datasetId)
        {
            // 獲取該資料集的所有剩餘批次
            var batchIds = await GetSuccessfulBatchIdsAsync(datasetId);
            
            // 重新計算每個批次的統計資料
            foreach (var batchId in batchIds)
            {
                await InsertByBatchAsync(datasetId, batchId);
            }
        }

        /// <summary>
        /// 重新建立最終統計資料
        /// </summary>
        public async Task RebuildFinalMetricsAsync(long datasetId)
        {
            // 重新聚合所有 by-batch 統計資料到最終表
            const string sql = @"
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
            
            await _sql.ExecAsync(sql, new { datasetId });
        }

        /// <summary>
        /// 更新批次狀態為成功
        /// </summary>
        public async Task<int> UpdateBatchStatusToSucceededAsync(long batchId)
        {
            const string sql = @"
                UPDATE dataset_batches 
                SET status='Succeeded', updated_at=NOW() 
                WHERE id=@BatchId";
            
            return await _sql.ExecAsync(sql, new { BatchId = batchId });
        }

        /// <summary>
        /// 獲取指定資料集的所有成功批次 ID
        /// </summary>
        public async Task<IReadOnlyList<long>> GetSuccessfulBatchIdsAsync(long datasetId)
        {
            const string sql = @"
                SELECT id FROM dataset_batches 
                WHERE dataset_id = @DatasetId AND status = 'Succeeded'
                ORDER BY id;";
            
            return await _sql.QueryAsync<long>(sql, new { DatasetId = datasetId });
        }
    }
}

