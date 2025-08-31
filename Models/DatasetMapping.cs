using BIDashboardBackend.Enums;

namespace BIDashboardBackend.Models
{
    public sealed class DatasetMapping
    {
        public long Id { get; init; }
        public long BatchId { get; init; }
        public string SourceColumn { get; init; } = string.Empty;
        public SystemField SystemField { get; init; }
    }
}
