namespace BIDashboardBackend.DTOs.Request
{
    /// <summary>
    /// 創建資料集的請求 DTO
    /// </summary>
    public class CreateDatasetDto
    {
        /// <summary>
        /// 資料集名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 資料集描述（可選）
        /// </summary>
        public string? Description { get; set; }
    }
}
