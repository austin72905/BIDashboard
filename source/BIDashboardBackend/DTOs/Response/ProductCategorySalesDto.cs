namespace BIDashboardBackend.DTOs.Response
{
    public sealed class ProductCategorySalesPoint
    {
        public string Category { get; init; } = string.Empty;
        public long Qty { get; init; }
    }

    public sealed class ProductCategorySalesDto
    {
        public long DatasetId { get; init; }
        public IReadOnlyList<ProductCategorySalesPoint> Points { get; init; } = new List<ProductCategorySalesPoint>();
        public string Unit { get; init; } = "quantity";
    }
}
