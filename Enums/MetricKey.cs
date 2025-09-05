namespace BIDashboardBackend.Enums
{
    /*
        定義 「報表指標」（Dashboard 要展示的 KPI）。

        目的是讓程式能夠一致地存取和查詢「哪一種指標」。
     
     
     */
    public enum MetricKey
    {
        TotalRevenue,            // 總營收
        TotalCustomers,          // 總客戶數
        TotalOrders,             // 總訂單數
        AvgOrderValue,           // 平均客單價
        NewCustomers,            // 新客戶
        ReturningCustomers,      // 回購客戶
        PendingOrders,           // 處理中訂單
        Regions,                 // 服務地區分布
        ProductCategorySales,    // 產品類別銷量
        MonthlyRevenueTrend,     // 月營收趨勢
        RegionDistribution,      // 地區分布 (同 Regions; 可合併用 bucket)
        AgeDistribution,         // 年齡分布統計
        GenderShare,             // 性別比例
        DataSummary              // 數據摘要（打包 KPI）
    }

    public static class MetricCatalog
    {
        public static readonly Dictionary<MetricKey, AggregationKind> Map = new()
        {
            { MetricKey.TotalRevenue, AggregationKind.Sum },
            { MetricKey.TotalCustomers, AggregationKind.DistinctCount },
            { MetricKey.TotalOrders, AggregationKind.Count },
            { MetricKey.AvgOrderValue, AggregationKind.Average },
            { MetricKey.NewCustomers, AggregationKind.DistinctCount },
            { MetricKey.ReturningCustomers, AggregationKind.DistinctCount },
            { MetricKey.PendingOrders, AggregationKind.StatusCount },
            { MetricKey.Regions, AggregationKind.Share },
            { MetricKey.ProductCategorySales, AggregationKind.Sum },
            { MetricKey.MonthlyRevenueTrend, AggregationKind.Sum },
            { MetricKey.RegionDistribution, AggregationKind.Share },
            { MetricKey.AgeDistribution, AggregationKind.Count },
            { MetricKey.GenderShare, AggregationKind.Share },
            { MetricKey.DataSummary, AggregationKind.Sum } // 或視情況打包計算
        };
    }



}
