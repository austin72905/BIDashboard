using BIDashboardBackend.Caching;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Utils;
using System.Text.Json;

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

        public async Task<KpiSummaryDto> GetKpiSummaryAsync(long datasetId)
        {
            var cacheKey = _keys.MetricKey(datasetId, "kpi-summary");
            
            // 優先從快取取資料
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<KpiSummaryDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            // 並行獲取所有 KPI
            var totalRevenueTask = _repo.GetTotalRevenueAsync(datasetId);
            var totalCustomersTask = _repo.GetTotalCustomersAsync(datasetId);
            var totalOrdersTask = _repo.GetTotalOrdersAsync(datasetId);
            var avgOrderValueTask = _repo.GetAvgOrderValueAsync(datasetId);
            var newCustomersTask = _repo.GetNewCustomersAsync(datasetId, DateTime.Now.AddMonths(-1));
            var returningCustomersTask = _repo.GetReturningCustomersAsync(datasetId, DateTime.Now.AddMonths(-1));
            var pendingOrdersTask = _repo.GetPendingOrdersAsync(datasetId);

            await Task.WhenAll(
                totalRevenueTask,
                totalCustomersTask,
                totalOrdersTask,
                avgOrderValueTask,
                newCustomersTask,
                returningCustomersTask,
                pendingOrdersTask
            );

            var result = new KpiSummaryDto
            {
                DatasetId = datasetId,
                TotalRevenue = await totalRevenueTask,
                TotalCustomers = await totalCustomersTask,
                TotalOrders = await totalOrdersTask,
                AvgOrderValue = await avgOrderValueTask,
                NewCustomers = await newCustomersTask,
                ReturningCustomers = await returningCustomersTask,
                PendingOrders = await pendingOrdersTask,
                UpdatedAt = DateTime.UtcNow
            };

            var jsonResult = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync(cacheKey, jsonResult, TimeSpan.FromMinutes(10));
            return result;
        }

        public async Task<AgeDistributionDto> GetAgeDistributionAsync(long datasetId)
        {
            var cacheKey = _keys.MetricKey(datasetId, "age-distribution");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<AgeDistributionDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetAgeDistributionAsync(datasetId);
            var result = new AgeDistributionDto
            {
                DatasetId = datasetId,
                Points = data.Select(x => new AgeDistributionPoint
                {
                    Bucket = x.Bucket,
                    Value = x.Value
                }).ToList()
            };

            var jsonResult = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync(cacheKey, jsonResult, TimeSpan.FromMinutes(15));
            return result;
        }

        public async Task<GenderShareDto> GetGenderShareAsync(long datasetId)
        {
            var cacheKey = _keys.MetricKey(datasetId, "gender-share");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<GenderShareDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetGenderShareAsync(datasetId);
            var result = new GenderShareDto
            {
                DatasetId = datasetId,
                Male = data.FirstOrDefault(x => x.Gender.Equals("male", StringComparison.OrdinalIgnoreCase)).Value,
                Female = data.FirstOrDefault(x => x.Gender.Equals("female", StringComparison.OrdinalIgnoreCase)).Value,
                Other = data.Where(x => !x.Gender.Equals("male", StringComparison.OrdinalIgnoreCase) 
                                     && !x.Gender.Equals("female", StringComparison.OrdinalIgnoreCase))
                           .Sum(x => x.Value)
            };

            var jsonResult = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync(cacheKey, jsonResult, TimeSpan.FromMinutes(15));
            return result;
        }

        public async Task<MonthlyRevenueTrendDto> GetMonthlyRevenueTrendAsync(long datasetId, int months = 12)
        {
            var cacheKey = _keys.MetricKey(datasetId, $"monthly-revenue-trend-{months}");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<MonthlyRevenueTrendDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetMonthlyRevenueTrendAsync(datasetId, months);
            var result = new MonthlyRevenueTrendDto
            {
                DatasetId = datasetId,
                Points = data.Select(x => new TrendPointDto
                {
                    Period = x.Period,
                    Value = x.Value
                }).ToList()
            };

            var jsonResult = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync(cacheKey, jsonResult, TimeSpan.FromMinutes(10));
            return result;
        }

        public async Task<RegionDistributionDto> GetRegionDistributionAsync(long datasetId)
        {
            var cacheKey = _keys.MetricKey(datasetId, "region-distribution");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<RegionDistributionDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetRegionDistributionAsync(datasetId);
            var result = new RegionDistributionDto
            {
                DatasetId = datasetId,
                Points = data.Select(x => new RegionDistributionPoint
                {
                    Name = x.Name,
                    Value = x.Value
                }).ToList()
            };

            var jsonResult = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync(cacheKey, jsonResult, TimeSpan.FromMinutes(15));
            return result;
        }

        public async Task<ProductCategorySalesDto> GetProductCategorySalesAsync(long datasetId)
        {
            var cacheKey = _keys.MetricKey(datasetId, "product-category-sales");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<ProductCategorySalesDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetProductCategorySalesAsync(datasetId);
            var result = new ProductCategorySalesDto
            {
                DatasetId = datasetId,
                Points = data.Select(x => new ProductCategorySalesPoint
                {
                    Category = x.Category,
                    Qty = x.Qty
                }).ToList()
            };

            var jsonResult = JsonSerializer.Serialize(result);
            await _cache.SetStringAsync(cacheKey, jsonResult, TimeSpan.FromMinutes(15));
            return result;
        }

        public async Task RemoveDatasetCacheAsync(long datasetId)
        {
            var prefix = _keys.MetricPrefix(datasetId);
            await _cache.RemoveByPrefixAsync(prefix);
        }
    }
}