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
        /// 資料欄位列表，來自上傳的 CSV 檔案
        /// </summary>
        public IReadOnlyList<DatasetColumn> DataColumns { get; init; }
        
    }
}
