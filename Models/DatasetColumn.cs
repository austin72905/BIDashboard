using System.ComponentModel.DataAnnotations.Schema;

namespace BIDashboardBackend.Models
{
    [Table("dataset_columns")]
    public sealed class DatasetColumn
    {
        public long? Id { get; set; }
        public long BatchId { get; set; }
        // // UNIQUE (batch_id, source_name)
        public string SourceName { get; set; } = default!;
        public string? DataType { get; set; }

        //UI提示，不參與聚合計算，只用來 UI 提示 / Debug
        public string? SampleValue { get; set; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
