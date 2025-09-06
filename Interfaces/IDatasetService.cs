using System.Collections.Generic;
using System.Threading.Tasks;

namespace BIDashboardBackend.Interfaces
{
    /// <summary>
    /// 資料集相關服務介面，提供資料集查詢功能
    /// </summary>
    public interface IDatasetService
    {
        /// <summary>
        /// 取得指定用戶可使用的所有資料集 ID
        /// </summary>
        /// <param name="userId">用戶 ID</param>
        /// <returns>該用戶可使用的資料集 ID 列表</returns>
        Task<IReadOnlyList<long>> GetDatasetIdsAsync(long userId);
    }
}
