namespace BIDashboardBackend.DTOs.Response
{
    /// <summary>
    /// 上傳歷史紀錄 DTO
    /// </summary>
    public sealed class UploadHistoryDto
    {
        public long BatchId { get; set; }
        public long DatasetId { get; set; }
        public string DatasetName { get; set; } = string.Empty;
        public string SourceFilename { get; set; } = string.Empty;
        public long TotalRows { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public IReadOnlyList<UploadHistoryColumnDto> Columns { get; set; } = new List<UploadHistoryColumnDto>();
    }

    /// <summary>
    /// 上傳歷史紀錄中的欄位資訊 DTO
    /// </summary>
    public sealed class UploadHistoryColumnDto
    {
        public string SourceName { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }
        public string? SystemField { get; set; } // 對應的系統欄位名稱
    }
}
