using BIDashboardBackend.Caching;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace BIDashboardBackend.Features.Jobs
{
    /// <summary>
    /// 負責執行資料集批次 ETL 的 Hangfire 工作
    /// </summary>
    public sealed class EtlJob : IEtlJob
    {
        // 交易相關的服務
        private readonly IUnitOfWork _uow;
        
        // ETL 資料處理的 Repository
        private readonly IEtlRepository _etlRepo;

        // 清除快取所需的工具與服務
        private readonly CacheKeyBuilder _keyBuilder;
        private readonly ICacheService _cache;
        
        // 日誌服務
        private readonly ILogger<EtlJob> _logger;

        /// <summary>
        /// 建構子：注入資料庫與快取相關服務
        /// </summary>
        public EtlJob(IUnitOfWork uow, IEtlRepository etlRepo, CacheKeyBuilder keyBuilder, ICacheService cache, ILogger<EtlJob> logger)
        {
            _uow = uow;
            _etlRepo = etlRepo;
            _keyBuilder = keyBuilder;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// 處理單一批次的 ETL 流程
        /// </summary>
        /// <param name="datasetId">資料集識別碼</param>
        /// <param name="batchId">批次識別碼，-1表示重新計算整個資料集</param>
        [AutomaticRetry(Attempts = 3)]
        public async Task ProcessBatch(long datasetId, long batchId)
        {
            var startTime = DateTime.UtcNow;
            _logger.LogInformation("開始處理批次 - DatasetId: {DatasetId}, BatchId: {BatchId}, 時間: {StartTime}", 
                datasetId, batchId, startTime);

            try
            {
                // 以交易包住整個流程，確保要嘛全部成功、要嘛全部回滾
                await _uow.BeginAsync();
                _logger.LogDebug("已開始資料庫交易 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);

                if (batchId == -1)
                {
                    _logger.LogInformation("重新計算整個資料集 - DatasetId: {DatasetId}", datasetId);
                    // 重新計算整個資料集
                    //await RebuildEntireDatasetAsync(datasetId);
                    await UpsertFinalForAffectedAsync(datasetId, batchId);
                    _logger.LogInformation("重新計算整個資料集完成 - DatasetId: {DatasetId}", datasetId);
                }
                else
                {
                    // A) by-batch：刪除舊資料並依映射重新計算
                    _logger.LogDebug("開始 RebuildByBatch - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
                    await RebuildByBatchAsync(datasetId, batchId);
                    _logger.LogInformation("RebuildByBatch 成功 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);

                    // B) final：重新計算整個 dataset 的所有指標
                    _logger.LogDebug("開始 UpsertFinalForAffected - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
                    await UpsertFinalForAffectedAsync(datasetId, batchId);
                    _logger.LogInformation("UpsertFinalForAffected 成功 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);

                    // C) 標記批次處理成功
                    _logger.LogDebug("更新批次狀態為成功 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
                    await _etlRepo.UpdateBatchStatusToSucceededAsync(batchId);
                    _logger.LogInformation("Dataset batch 更新狀態成功 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
                }

                // D) 提交交易，寫入以上所有變更
                await _uow.CommitAsync();
                _logger.LogInformation("Commit 成功 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);

                // E) 清快取：清除該資料集相關的指標快取
                var cachePrefix = _keyBuilder.MetricPrefix(datasetId);
                await _cache.RemoveByPrefixAsync(cachePrefix);
                _logger.LogDebug("快取清除完成 - DatasetId: {DatasetId}, CachePrefix: {CachePrefix}", datasetId, cachePrefix);

                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;
                _logger.LogInformation("批次處理完成 - DatasetId: {DatasetId}, BatchId: {BatchId}, 耗時: {Duration}ms", 
                    datasetId, batchId, duration.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批次處理失敗，進行 rollback - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
                
                try
                {
                    await _uow.RollbackAsync();
                    _logger.LogInformation("Rollback 成功 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Rollback 失敗 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
                }
                
                throw; // 重新拋出原始異常
            }
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
            _logger.LogDebug("開始清除舊的 by-batch 統計資料 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
            await _etlRepo.ClearOldByBatchAsync(datasetId, batchId);
            _logger.LogDebug("清除舊的 by-batch 統計資料完成 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
        }

        /// <summary>
        /// 依映射解析資料列並寫入新的 by-batch 統計值
        /// </summary>
        private async Task InsertByBatchAsync(long datasetId, long batchId)
        {
            _logger.LogDebug("開始寫入新的 by-batch 統計值 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
            await _etlRepo.InsertByBatchAsync(datasetId, batchId);
            _logger.LogDebug("寫入新的 by-batch 統計值完成 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
        }

        /// <summary>
        /// 重新彙總受影響的切片並寫入最終統計表
        /// </summary>
        private async Task UpsertFinalMetricsAsync(long datasetId, long batchId)
        {
            _logger.LogDebug("開始更新最終統計表 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
            await _etlRepo.UpsertFinalMetricsAsync(datasetId, batchId);
            _logger.LogDebug("更新最終統計表完成 - DatasetId: {DatasetId}, BatchId: {BatchId}", datasetId, batchId);
        }

        /// <summary>
        /// 清除指定資料集的所有統計資料
        /// </summary>
        private async Task ClearAllMetricsForDatasetAsync(long datasetId)
        {
            await _etlRepo.ClearAllMetricsForDatasetAsync(datasetId);
        }

        /// <summary>
        /// 重新計算指定資料集的所有剩餘批次
        /// </summary>
        private async Task RecalculateAllBatchesAsync(long datasetId)
        {
            await _etlRepo.RecalculateAllBatchesAsync(datasetId);
        }

        /// <summary>
        /// 重新建立最終統計資料
        /// </summary>
        private async Task RebuildFinalMetricsAsync(long datasetId)
        {
            await _etlRepo.RebuildFinalMetricsAsync(datasetId);
        }
    }
}
