namespace BIDashboardBackend.DTOs.Response
{
    public sealed class GenderShareDto
    {
        public long DatasetId { get; init; }
        public long Male { get; init; }
        public long Female { get; init; }
        public long Other { get; init; }
    }
}
