using BIDashboardBackend.Enums;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;

namespace BIDashboardBackend.Repositories
{
    public sealed class MetricRepository : IMetricRepository
    {
        private readonly ISqlRunner _sql;
        public MetricRepository(ISqlRunner sql) => _sql = sql;

        // 將 enum 轉為資料表中的 int metric_key
        private static int Key(MetricKey k) => (int)k;

        // ===== KPI =====

        public async Task<decimal> GetTotalRevenueAsync(long datasetId,long userId)
        {
            // revenue 可能被切成多 bucket（例如月份），統計時以 SUM(value)
            const string sql = @"
                SELECT COALESCE(SUM(mm.value), 0)::numeric
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey 
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                );";
            var v = await _sql.ScalarAsync<decimal>(sql, new { datasetId, userId ,metricKey = Key(MetricKey.TotalRevenue) });
            return v;
        }

        public async Task<long> GetTotalCustomersAsync(long datasetId, long userId)
        {
            const string sql = @"
                SELECT COALESCE(SUM(mm.value), 0)::bigint
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                );";
            var v = await _sql.ScalarAsync<long>(sql, new { datasetId, userId, metricKey = Key(MetricKey.TotalCustomers) });
            return v;
        }

        public async Task<long> GetTotalOrdersAsync(long datasetId, long userId)
        {
            const string sql = @"
                SELECT COALESCE(SUM(mm.value), 0)::bigint
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                );";
            var v = await _sql.ScalarAsync<long>(sql, new { datasetId, userId, metricKey = Key(MetricKey.TotalOrders) });
            return v;
        }

        public async Task<decimal> GetAvgOrderValueAsync(long datasetId, long userId)
        {
            // 從 materialized_metrics 直接取 AvgOrderValue
            const string sql = @"
                SELECT COALESCE(SUM(mm.value), 0)::numeric
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                );";
            var v = await _sql.ScalarAsync<decimal>(sql, new { datasetId, userId, metricKey = Key(MetricKey.AvgOrderValue) });
            return v;
        }

        public async Task<long> GetNewCustomersAsync(long datasetId, DateTime since, long userId)
        {
            // 使用 DateTime 取代 DateOnly
            const string sql = @"
                SELECT COALESCE(SUM(mm.value),0)::bigint
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId
                  AND mm.metric_key = @metricKey
                  AND mm.period >= @since::date
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                );";
            var v = await _sql.ScalarAsync<long>(sql, new { datasetId, userId, metricKey = Key(MetricKey.NewCustomers), since });
            return v;
        }

        public async Task<long> GetReturningCustomersAsync(long datasetId, DateTime since, long userId)
        {
            // 使用 DateTime 取代 DateOnly
            const string sql = @"
                SELECT COALESCE(SUM(mm.value),0)::bigint
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId
                  AND mm.metric_key = @metricKey
                  AND mm.period >= @since::date
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                );";
            var v = await _sql.ScalarAsync<long>(sql, new { datasetId, userId, metricKey = Key(MetricKey.ReturningCustomers), since });
            return v;
        }

        public async Task<long> GetPendingOrdersAsync(long datasetId, long userId)
        {
            const string sql = @"
                SELECT COALESCE(SUM(mm.value),0)::bigint
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                );";
            var v = await _sql.ScalarAsync<long>(sql, new { datasetId, userId, metricKey = Key(MetricKey.PendingOrders) });
            return v;
        }

        // ===== 趨勢 / 分布 =====

        private sealed class PeriodValueRow { public DateTime Period { get; init; } public decimal Value { get; init; } }
        private sealed class NameValueRow { public string Name { get; init; } = string.Empty; public long Value { get; init; } }
        private sealed class CatQtyRow { public string Category { get; init; } = string.Empty; public long Qty { get; init; } }
        private sealed class BucketRow { public string Bucket { get; init; } = string.Empty; public long Value { get; init; } }

        public async Task<IReadOnlyList<(DateTime Period, decimal Value)>> GetMonthlyRevenueTrendAsync(long datasetId, int months, long userId)
        {
            const string sql = @"
                SELECT period AS Period,
                       COALESCE(SUM(value),0)::numeric AS Value
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey
                  AND mm.period IS NOT NULL
                  AND mm.period >= (date_trunc('month', CURRENT_DATE) - (@months::int - 1) * INTERVAL '1 month')::date
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                )
                GROUP BY Period
                ORDER BY Period;";
            var rows = await _sql.QueryAsync<PeriodValueRow>(sql, new
            {
                datasetId,
                userId,
                metricKey = Key(MetricKey.MonthlyRevenueTrend),
                months
            });
            return rows.Select(r => (r.Period, r.Value)).ToList();
        }

        public async Task<IReadOnlyList<(string Name, long Value)>> GetRegionDistributionAsync(long datasetId, long userId)
        {
            const string sql = @"
                SELECT bucket AS Name, COALESCE(SUM(value),0)::bigint AS Value
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey
                  AND mm.bucket IS NOT NULL
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                )
                GROUP BY bucket
                ORDER BY Value DESC, bucket;";
            var rows = await _sql.QueryAsync<NameValueRow>(sql, new
            {
                datasetId,
                userId,
                metricKey = Key(MetricKey.RegionDistribution)
            });
            return rows.Select(r => (r.Name, r.Value)).ToList();
        }

        public async Task<IReadOnlyList<(string Category, long Qty)>> GetProductCategorySalesAsync(long datasetId, long userId)
        {
            const string sql = @"
                SELECT bucket AS Category, COALESCE(SUM(value),0)::bigint AS Qty
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey
                  AND mm.bucket IS NOT NULL
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                )
                GROUP BY bucket
                ORDER BY Qty DESC, bucket;";
            var rows = await _sql.QueryAsync<CatQtyRow>(sql, new
            {
                datasetId,
                userId,
                metricKey = Key(MetricKey.ProductCategorySales)
            });
            return rows.Select(r => (r.Category, r.Qty)).ToList();
        }

        public async Task<IReadOnlyList<(string Bucket, long Value)>> GetAgeDistributionAsync(long datasetId, long userId)
        {
            const string sql = @"
                SELECT bucket, COALESCE(SUM(value),0)::bigint AS Value
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey
                  AND mm.bucket IS NOT NULL
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                )
                GROUP BY bucket
                ORDER BY bucket;"; // 例如 '20-29','30-39',...
            var rows = await _sql.QueryAsync<BucketRow>(sql, new
            {
                datasetId,
                userId,
                metricKey = Key(MetricKey.AgeDistribution)
            });
            return rows.Select(r => (r.Bucket, r.Value)).ToList();
        }

        public async Task<IReadOnlyList<(string Gender, long Value)>> GetGenderShareAsync(long datasetId, long userId)
        {
            const string sql = @"
                SELECT bucket AS Gender, COALESCE(SUM(value),0)::bigint AS Value
                FROM materialized_metrics mm
                WHERE mm.dataset_id = @datasetId AND mm.metric_key = @metricKey
                  AND mm.bucket IS NOT NULL
                AND EXISTS(
                    SELECT 1 FROM datasets d
                    WHERE d.id = @datasetId AND d.owner_id = @userId
                )
                GROUP BY bucket
                ORDER BY bucket;";
            var rows = await _sql.QueryAsync<BucketRow>(sql, new
            {
                datasetId,
                userId,
                metricKey = Key(MetricKey.GenderShare)
            });
            return rows.Select(r => (r.Bucket, r.Value)).ToList();
        }

        // ===== 寫入 / 覆蓋 =====

        public Task UpsertMetricAsync(long datasetId, MetricKey key, string bucket, decimal value)
        {
            const string sql = @"
                INSERT INTO materialized_metrics (dataset_id, metric_key, bucket, value, updated_at)
                VALUES (@datasetId, @metricKey, @bucket, @value, NOW())
                ON CONFLICT (dataset_id, metric_key, bucket, period)
                DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();";
            return _sql.ExecAsync(sql, new
            {
                datasetId,
                metricKey = Key(key),
                bucket,
                value
            });
        }

        public async Task BulkReplaceMetricAsync(long datasetId, MetricKey key, IEnumerable<(string bucket, decimal value)> rows)
        {
            // 先刪後灌（覆蓋語意）
            const string del = @"
                DELETE FROM materialized_metrics
                WHERE dataset_id = @datasetId AND metric_key = @metricKey;";
            await _sql.ExecAsync(del, new { datasetId, metricKey = Key(key) });

            // 空集合就結束
            var list = rows as IList<(string bucket, decimal value)> ?? rows.ToList();
            if (list.Count == 0) return;

            var buckets = new List<string>(list.Count);
            var values = new List<decimal>(list.Count);
            foreach (var (b, v) in list)
            {
                buckets.Add(b);
                values.Add(v);
            }

            // UNNEST 批量插入
            const string ins = @"
                INSERT INTO materialized_metrics (dataset_id, metric_key, bucket, value, updated_at)
                SELECT @datasetId::bigint, @metricKey::int, t.b::text, t.v::numeric, NOW()
                FROM UNNEST(@buckets::text[], @values::numeric[]) AS t(b, v);";
            await _sql.ExecAsync(ins, new
            {
                datasetId,
                metricKey = Key(key),
                buckets = buckets.ToArray(),
                values = values.ToArray()
            });
        }
    }

}