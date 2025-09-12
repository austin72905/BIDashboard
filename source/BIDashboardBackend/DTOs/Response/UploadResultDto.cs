namespace BIDashboardBackend.DTOs.Response
{
    public sealed class UploadResultDto
    {
        public long BatchId { get; init; }
        public string FileName { get; init; } = string.Empty;
        public long TotalRows { get; init; }
        public string Status { get; init; } = "Pending";
    }
}
