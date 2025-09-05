using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.DTOs.Response;
using BIDashboardBackend.Models;

namespace BIDashboardBackend.Interfaces
{
    public interface IIngestService
    {
        Task<UploadResultDto> UploadCsvAsync(IFormFile file, long userId);
        Task UpsertMappingsAsync(UpsertMappingsRequestDto request);
        Task<IReadOnlyList<DatasetColumn>> GetColumnsAsync(long batchId);
        Task<ColumnMappingInfoDto> GetColumnMappingInfoAsync(long batchId);
        
        /// <summary>
        /// 獲取用戶的上傳歷史紀錄
        /// </summary>
        /// <param name="userId">用戶 ID</param>
        /// <param name="limit">限制筆數，預設 50</param>
        /// <param name="offset">偏移量，預設 0</param>
        /// <returns>上傳歷史紀錄列表</returns>
        Task<IReadOnlyList<UploadHistoryDto>> GetUploadHistoryAsync(long userId, int limit = 50, int offset = 0);
        
        /// <summary>
        /// 獲取指定批次的詳細資訊（包含欄位和映射）
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <param name="userId">用戶 ID（用於權限驗證）</param>
        /// <returns>批次詳細資訊</returns>
        Task<UploadHistoryDto?> GetBatchDetailsAsync(long batchId, long userId);
    }
}
