namespace BIDashboardBackend.DTOs.Response
{
    public sealed class GenderShareDto
    {
        public long DatasetId { get; init; }
        public decimal Male { get; init; } // 男性比例 (0-1)
        public decimal Female { get; init; } // 女性比例 (0-1)
        public decimal Other { get; init; } // 其他比例 (0-1)
    }
}
