namespace BIDashboardBackend.DTOs.Response
{
    /// <summary>
    /// 創建資料集的響應 DTO
    /// </summary>
    public class CreateDatasetResultDto
    {
        /// <summary>
        /// 資料集 ID
        /// </summary>
        public long DatasetId { get; set; }
        
        /// <summary>
        /// 資料集名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// 資料集描述
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// 創建時間
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
