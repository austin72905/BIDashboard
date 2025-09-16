using BIDashboardBackend.Caching;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Enums;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;
using BIDashboardBackend.Utils;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace BIDashboardBackend.Services
{
    public sealed class MetricService : IMetricService
    {
        private readonly IMetricRepository _repo;
        private readonly ICacheService _cache;
        private readonly CacheKeyBuilder _keys;
        private readonly ILogger<MetricService> _logger;

        public MetricService(IMetricRepository repo, ICacheService cache, CacheKeyBuilder keys, ILogger<MetricService> logger)
        {
            _repo = repo;
            _cache = cache;
            _keys = keys;
            _logger = logger;
        }

        

        public async Task<KpiSummaryDto> GetKpiSummaryAsync(long datasetId, long userId)
        {
            var stopwatch = Stopwatch.StartNew();
            var cacheKey = _keys.MetricKey(datasetId, "kpi-summary");
            
            _logger.LogDebug("開始取得 KPI 摘要 - DatasetId: {DatasetId}, UserId: {UserId}, CacheKey: {CacheKey}", 
                datasetId, userId, cacheKey);
            
            // 優先從快取取資料
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<KpiSummaryDto>(cached);
                if (cachedResult != null) 
                {
                    stopwatch.Stop();
                    _logger.LogInformation("快取命中 - DatasetId: {DatasetId}, Method: GetKpiSummaryAsync, 耗時: {ElapsedMs}ms", 
                        datasetId, stopwatch.ElapsedMilliseconds);
                    return cachedResult;
                }
            }

            _logger.LogInformation("快取未命中，從資料庫查詢 - DatasetId: {DatasetId}, Method: GetKpiSummaryAsync", datasetId);

            // 並行獲取所有 KPI
            var totalRevenueTask = _repo.GetTotalRevenueAsync(datasetId, userId);
            var totalCustomersTask = _repo.GetTotalCustomersAsync(datasetId, userId);
            var totalOrdersTask = _repo.GetTotalOrdersAsync(datasetId, userId);
            var avgOrderValueTask = _repo.GetAvgOrderValueAsync(datasetId, userId);
            var newCustomersTask = _repo.GetNewCustomersAsync(datasetId, DateTime.Now.AddMonths(-1), userId);
            var returningCustomersTask = _repo.GetReturningCustomersAsync(datasetId, DateTime.Now.AddMonths(-1), userId);
            var pendingOrdersTask = _repo.GetPendingOrdersAsync(datasetId, userId);

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
            
            stopwatch.Stop();
            _logger.LogInformation("資料庫查詢完成並寫入快取 - DatasetId: {DatasetId}, Method: GetKpiSummaryAsync, 耗時: {ElapsedMs}ms", 
                datasetId, stopwatch.ElapsedMilliseconds);
            
            return result;
        }

        public async Task<AgeDistributionDto> GetAgeDistributionAsync(long datasetId, long userId)
        {
            var cacheKey = _keys.MetricKey(datasetId, "age-distribution");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<AgeDistributionDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetAgeDistributionAsync(datasetId, userId);
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

        public async Task<GenderShareDto> GetGenderShareAsync(long datasetId, long userId)
        {
            var cacheKey = _keys.MetricKey(datasetId, "gender-share");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<GenderShareDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetGenderShareAsync(datasetId, userId);
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

        public async Task<MonthlyRevenueTrendDto> GetMonthlyRevenueTrendAsync(long datasetId, long userId, int months = 12)
        {
            var cacheKey = _keys.MetricKey(datasetId, $"monthly-revenue-trend-{months}");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<MonthlyRevenueTrendDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetMonthlyRevenueTrendAsync(datasetId, months, userId);
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

        public async Task<RegionDistributionDto> GetRegionDistributionAsync(long datasetId, long userId)
        {
            var cacheKey = _keys.MetricKey(datasetId, "region-distribution");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<RegionDistributionDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetRegionDistributionAsync(datasetId, userId);
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

        public async Task<ProductCategorySalesDto> GetProductCategorySalesAsync(long datasetId, long userId)
        {
            var cacheKey = _keys.MetricKey(datasetId, "product-category-sales");
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedResult = JsonSerializer.Deserialize<ProductCategorySalesDto>(cached);
                if (cachedResult != null) return cachedResult;
            }

            var data = await _repo.GetProductCategorySalesAsync(datasetId, userId);
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



        public async Task<AllMetricsDto> GetAllMetricsAsync(
            long datasetId, long userId, int months = 12)
        {
            var stopwatch = Stopwatch.StartNew();
            
            // 建議把 months 放進快取 key，避免不同視窗互覆
            var cacheKey = _keys.MetricKey(datasetId, $"all-metrics:m{months}");
            
            _logger.LogDebug("開始取得所有指標 - DatasetId: {DatasetId}, UserId: {UserId}, Months: {Months}, CacheKey: {CacheKey}", 
                datasetId, userId, months, cacheKey);
            
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var ok = JsonSerializer.Deserialize<AllMetricsDto>(cached);
                if (ok is not null) 
                {
                    stopwatch.Stop();
                    _logger.LogInformation("快取命中 - DatasetId: {DatasetId}, Method: GetAllMetricsAsync, 耗時: {ElapsedMs}ms", 
                        datasetId, stopwatch.ElapsedMilliseconds);
                    return ok;
                }
            }

            _logger.LogInformation("快取未命中，從資料庫查詢 - DatasetId: {DatasetId}, Method: GetAllMetricsAsync", datasetId);

            // 一次查回所有需要的度量資料（單次往返、單連線）
            var rows = await _repo.GetAllMetricsRowsAsync(datasetId, userId, months);

            // 小工具
            decimal SumDec(string metric) => rows.Where(r => r.Metric == metric)
                                                 .Sum(r => r.Value);
            long SumLong(string metric) => (long)Math.Round(SumDec(metric));

            var now = DateTime.UtcNow;

            // 判斷是否有 customerid mapping（通過檢查 TotalCustomers 是否為 0）
            var totalCustomersRaw = SumLong("TotalCustomers");
            var hasCustomerMapping = totalCustomersRaw > 0;

            // KPI 組裝
            var kpi = new KpiSummaryDto
            {
                DatasetId = datasetId,
                TotalRevenue = SumDec("TotalRevenue"),
                // 根據是否有 customerid mapping 決定總客戶數的計算方式
                TotalCustomers = hasCustomerMapping ? 
                    totalCustomersRaw : // 有 mapping：使用去重後的客戶數
                    SumLong("TotalOrders"), // 沒有 mapping：使用總筆數（訂單數）
                TotalOrders = SumLong("TotalOrders"),
                AvgOrderValue = SumDec("AvgOrderValue"),
                NewCustomers = SumLong("NewCustomers"),
                ReturningCustomers = SumLong("ReturningCustomers"),
                PendingOrders = SumLong("PendingOrders"),
                UpdatedAt = now
            };

            // 月營收趨勢
            var monthlyTrend = new MonthlyRevenueTrendDto
            {
                DatasetId = datasetId,
                Points = rows.Where(r => r.Metric == "MonthlyRevenueTrend" && r.Period.HasValue)
                             .OrderBy(r => r.Period)
                             .Select(r => new TrendPointDto { Period = r.Period!.Value, Value = r.Value })
                             .ToList()
            };

            // 地區分布
            // 注意：RegionDistribution 的 value 是比例值（0-1），直接轉換為百分比
            var regionRows = rows.Where(r => r.Metric == "RegionDistribution" && r.Bucket != null).ToList();
            var region = new RegionDistributionDto
            {
                DatasetId = datasetId,
                Points = regionRows.Any() ? 
                    regionRows.OrderByDescending(r => r.Value).ThenBy(r => r.Bucket)
                              .Select(r => new RegionDistributionPoint
                              {
                                  Name = r.Bucket!,
                                  // 直接返回百分比（0-1 轉換為 0-100）
                                  Value = (long)Math.Round(r.Value * 100, 2)
                              })
                              .ToList() : 
                    new List<RegionDistributionPoint>()
            };

            // 產品類別銷量
            var product = new ProductCategorySalesDto
            {
                DatasetId = datasetId,
                Points = rows.Where(r => r.Metric == "ProductCategorySales" && r.Bucket != null)
                             .OrderByDescending(r => r.Value).ThenBy(r => r.Bucket)
                             .Select(r => new ProductCategorySalesPoint
                             {
                                 Category = r.Bucket!,
                                 Qty = (long)Math.Round(r.Value)
                             })
                             .ToList()
            };

            // 年齡分布
            var age = new AgeDistributionDto
            {
                DatasetId = datasetId,
                Points = rows.Where(r => r.Metric == "AgeDistribution" && r.Bucket != null)
                             .OrderBy(r => r.Bucket)
                             .Select(r => new AgeDistributionPoint
                             {
                                 Bucket = r.Bucket!,
                                 Value = (long)Math.Round(r.Value)
                             })
                             .ToList()
            };

            // 性別占比（直接使用比例值，不轉換為實際數量）
            decimal male = 0, female = 0, other = 0;
            var genderRows = rows.Where(r => r.Metric == "GenderShare" && r.Bucket != null).ToList();
            
            if (genderRows.Any())
            {
                foreach (var r in genderRows)
                {
                    var b = r.Bucket!.Trim().ToLowerInvariant();
                    // 直接使用比例值 (0-1)
                    var ratio = r.Value;
                    
                    if (b is "m" or "male" or "man" or "boy") male += ratio;
                    else if (b is "f" or "female" or "woman" or "girl") female += ratio;
                    else other += ratio;
                }
            }
            var gender = new GenderShareDto
            {
                DatasetId = datasetId,
                Male = male,
                Female = female,
                Other = other
            };

            var result = new AllMetricsDto
            {
                DatasetId = datasetId,
                KpiSummary = kpi,
                AgeDistribution = age,
                GenderShare = gender,
                MonthlyRevenueTrend = monthlyTrend,
                RegionDistribution = region,
                ProductCategorySales = product,
                UpdatedAt = now
            };

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result),
                                        TimeSpan.FromMinutes(5));
            
            stopwatch.Stop();
            _logger.LogInformation("資料庫查詢完成並寫入快取 - DatasetId: {DatasetId}, Method: GetAllMetricsAsync, 耗時: {ElapsedMs}ms", 
                datasetId, stopwatch.ElapsedMilliseconds);
            
            return result;
        }

        public async Task RemoveDatasetCacheAsync(long datasetId)
        {
            var prefix = _keys.MetricPrefix(datasetId);
            _logger.LogInformation("清除資料集快取 - DatasetId: {DatasetId}, CachePrefix: {CachePrefix}", datasetId, prefix);
            await _cache.RemoveByPrefixAsync(prefix);
            _logger.LogDebug("資料集快取清除完成 - DatasetId: {DatasetId}", datasetId);
        }

        
    }
}