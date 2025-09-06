using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Interfaces.Repositories;

namespace BIDashboardBackend.Services
{
    /// <summary>
    /// 資料集服務，封裝資料集的存取邏輯
    /// </summary>
    public sealed class DatasetService : IDatasetService
    {
        private readonly IDatasetRepository _repo;

        /// <summary>
        /// 建構子，注入資料集資料存取層
        /// </summary>
        public DatasetService(IDatasetRepository repo) => _repo = repo;

        /// <summary>
        /// 取得指定用戶可使用的所有資料集 ID
        /// </summary>
        /// <param name="userId">用戶 ID</param>
        /// <returns>該用戶可使用的資料集 ID 列表</returns>
        public Task<IReadOnlyList<long>> GetDatasetIdsAsync(long userId)
            => _repo.GetDatasetIdsByUserAsync(userId);
    }
}
