using BIDashboardBackend.Enums;
using BIDashboardBackend.Models;

namespace BIDashboardBackend.DTOs.Response
{
    public sealed class ColumnMappingInfoDto
    {
        /// <summary>
        /// 系統欄位字典，包含所有可用的系統欄位及其資訊
        /// </summary>
        public Dictionary<SystemField, SystemFieldInfo.SystemFieldProp> SystemFields { get; init; } = new();

        /// <summary>
        /// 資料欄位列表，包含映射資訊，來自上傳的 CSV 檔案
        /// </summary>
        public IReadOnlyList<DatasetColumnWithMapping> DataColumns { get; init; }
        
    }

    /// <summary>
    /// 包含映射資訊的資料欄位
    /// </summary>
    public sealed class DatasetColumnWithMapping
    {
        public long? Id { get; init; }
        public long BatchId { get; init; }
        public string SourceName { get; init; } = default!;
        public string? DataType { get; init; }
        public string? SampleValue { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
        
        /// <summary>
        /// 映射的系統欄位，如果未映射則為 null
        /// </summary>
        public SystemField? MappedSystemField { get; init; }
        
        /// <summary>
        /// 映射 ID，如果未映射則為 null
        /// </summary>
        public long? MappingId { get; init; }
        
        /// <summary>
        /// 映射建立時間，如果未映射則為 null
        /// </summary>
        public DateTime? MappingCreatedAt { get; init; }
    }
}
