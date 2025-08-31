namespace BIDashboardBackend.DTOs.Response
{
    public sealed class AgeDistributionPoint
    {
        public string Bucket { get; init; } = string.Empty; // e.g. "0-9","10-19"
        public long Value { get; init; }
    }


    public sealed class AgeDistributionDto
    {
        public long DatasetId { get; init; }
        public IReadOnlyList<AgeDistributionPoint> Points { get; init; } = new List<AgeDistributionPoint>();
        public string Unit { get; init; } = "people";
    }
}
