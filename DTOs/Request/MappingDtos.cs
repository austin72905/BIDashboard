namespace BIDashboardBackend.DTOs.Request
{
    public sealed class UpsertMappingsRequestDto
    {
        public long BatchId { get; init; }
        public List<SourceToSystemField> Mappings { get; init; } = new();
    }


    public sealed class SourceToSystemField
    {
        public string SourceColumn { get; init; } = string.Empty; // CSV 欄位名稱
        public SystemField SystemField { get; init; }
    }
}
