using BIDashboardBackend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BIDashboardBackend.Controllers
{
    /// <summary>
    /// 提供與目前用戶相關的查詢接口
    /// </summary>
    [ApiController]
    [Route("api/me")]
    [Authorize]
    public sealed class MeController : BaseController
    {
        private readonly IDatasetService _datasetService;

        /// <summary>
        /// 建構子，注入資料集服務以查詢用戶可用的資料集
        /// </summary>
        public MeController(IDatasetService datasetService) => _datasetService = datasetService;

        /// <summary>
        /// 取得目前用戶可使用的所有資料集 ID
        /// </summary>
        /// <returns>資料集 ID 列表</returns>
        [HttpGet("datasets")]
        public async Task<IActionResult> GetDatasetIds()
        {
            var ids = await _datasetService.GetDatasetIdsAsync(UserId);
            return Ok(ids);
        }
    }
}
