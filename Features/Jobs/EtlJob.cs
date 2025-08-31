using BIDashboardBackend.Caching;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Interfaces.Repositories;

namespace BIDashboardBackend.Features.Jobs
{
    public sealed class EtlJob : IEtlJob
    {
        private readonly IDatasetRepository _datasetRepo;
        private readonly IMetricRepository _metricRepo;
        private readonly CacheKeyBuilder _keyBuilder;
        private readonly ICacheService _cache;


        public EtlJob(IDatasetRepository datasetRepo, IMetricRepository metricRepo, CacheKeyBuilder keyBuilder, ICacheService cache)
        {
            _datasetRepo = datasetRepo;
            _metricRepo = metricRepo;
            _keyBuilder = keyBuilder;
            _cache = cache;
        }


        public async Task RunEtlForBatchAsync(long batchId, CancellationToken ct = default)
        {
            // TODO: 讀 dataset_rows + dataset_mappings 做清洗與聚合。
            // 以下為示意：計算年齡分布/性別占比並寫入 materialized_metrics。


            long datasetId = batchId; // MVP：以 batchId 當 datasetId


            // 假資料示意（請改為實際聚合結果）
            var age = new List<AgeDistributionPoint>
            {
                new() { Bucket = "0-9", Value = 10 },
                new() { Bucket = "10-19", Value = 20 }
            };

            var gender = new GenderShareDto { DatasetId = datasetId, Male = 15, Female = 12, Other = 3 };


            await _metricRepo.UpsertAgeDistributionAsync(datasetId, age);
            await _metricRepo.UpsertGenderShareAsync(datasetId, gender);


            // 失效該 dataset 相關快取
            await _cache.RemoveByPrefixAsync(_keyBuilder.MetricPrefix(datasetId));
        }
    }
}
