using BIDashboardBackend.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace BIDashboardBackend.Models
{
    // materialized_metrics（分析後的彙總結果；永遠維持 dataset 最新狀態）
    [Table("materialized_metrics")]
    public sealed class MaterializedMetric
    {
        public long Id { get; init; }
        public long DatasetId { get; init; }
        public MetricKey MetricKey { get; init; }  // age-distribution | gender-share
        public string? Bucket { get; init; } = string.Empty; // e.g. 0-9,10-19 or male,female

        public DateTime? Period { get; init; }
        // NUMERIC(20,4) → decimal
        public decimal Value { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
