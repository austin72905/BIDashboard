namespace BIDashboardBackend.Enums
{
    public enum AggregationKind
    {
        Sum,            // 總和
        Count,          // 簡單計數
        Average,        // 平均 (Σsum / Σcount)
        Share,          // 分布占比 (各 bucket / 總和)
        Ratio,          // 比率 (numer/denom)
        DistinctCount,  // 不重複計數
        StatusCount     // 特定狀態的筆數
    }
}
