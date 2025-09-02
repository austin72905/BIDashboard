namespace BIDashboardBackend.Models
{
    public sealed class DatasetBatch
    {
        public long Id { get; init; }

        public long? DatasetId { get; init; }                      // FK -> datasets.id
        public string SourceFilename { get; init; } = string.Empty;
        public long TotalRows { get; init; }

        // 狀態很少的用 text + check , 狀態多的用 int , 應用程式 用 enum
        public string Status { get; init; } = "Pending"; //建議值域：Pending | Mapped | Processing | Succeeded | Failed
        public string? ErrorMessage { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
