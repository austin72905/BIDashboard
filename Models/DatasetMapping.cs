using BIDashboardBackend.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace BIDashboardBackend.Models
{
    [Table("dataset_mappings")]
    public sealed class DatasetMapping
    {
        public long Id { get; init; }
        public long BatchId { get; init; }
        // FK (batch_id, source_name) -> dataset_columns
        public string SourceColumn { get; init; } = string.Empty;
        public SystemField SystemField { get; init; }

        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
