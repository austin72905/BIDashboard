namespace BIDashboardBackend.Models
{
    public sealed class DatasetColumn
    {
        public long Id { get; init; }
        public long BatchId { get; init; }
        public string ColumnName { get; init; } = string.Empty;
        public string DataType { get; init; } = string.Empty; // 推測型別
    }
}
