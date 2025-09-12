namespace BIDashboardBackend.DTOs.Response
{
    public sealed class RegionDistributionPoint
    {
        public string Name { get; init; } = string.Empty;
        public long Value { get; init; }
    }

    public sealed class RegionDistributionDto
    {
        public long DatasetId { get; init; }
        public IReadOnlyList<RegionDistributionPoint> Points { get; init; } = new List<RegionDistributionPoint>();
        public string Unit { get; init; } = "people";
    }
}
