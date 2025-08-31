namespace BIDashboardBackend.Models
{
    public sealed class DatasetBatch
    {
        public long Id { get; init; }
        public string SourceFilename { get; init; } = string.Empty;
        public long TotalRows { get; init; }
        public string Status { get; init; } = "Pending"; // Pending|Succeeded|Failed
        public string? ErrorMessage { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
