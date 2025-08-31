using BIDashboardBackend.Caching;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Utils;

namespace BIDashboardBackend.Services
{
    public sealed class MetricService : IMetricService
    {
        private readonly IMetricRepository _repo;
        private readonly ICacheService _cache;
        private readonly CacheKeyBuilder _keys;


        public MetricService(IMetricRepository repo, ICacheService cache, CacheKeyBuilder keys)
        {
            _repo = repo;
            _cache = cache;
            _keys = keys;
        }


        public async Task<AgeDistributionDto> GetAgeDistributionAsync(long datasetId, CancellationToken ct = default)
        {
            var key = _keys.Metric(datasetId, "age-distribution", "all");
            var cached = await _cache.GetStringAsync(key);
            if (cached is not null)
                return Json.Deserialize<AgeDistributionDto>(cached);


            var points = await _repo.GetAgeDistributionAsync(datasetId);
            var dto = new AgeDistributionDto { DatasetId = datasetId, Points = points.ToList() };
            await _cache.SetStringAsync(key, Json.Serialize(dto), TimeSpan.FromMinutes(10));
            return dto;
        }


        public async Task<GenderShareDto> GetGenderShareAsync(long datasetId, CancellationToken ct = default)
        {
            var key = _keys.Metric(datasetId, "gender-share", "all");
            var cached = await _cache.GetStringAsync(key);
            if (cached is not null)
                return Json.Deserialize<GenderShareDto>(cached);


            var dto = await _repo.GetGenderShareAsync(datasetId);
            await _cache.SetStringAsync(key, Json.Serialize(dto), TimeSpan.FromMinutes(10));
            return dto;
        }
    }
}
