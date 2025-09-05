using BIDashboardBackend.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BIDashboardBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MetricsController : BaseController
    {
        private readonly IMetricService _svc;
        public MetricsController(IMetricService svc) => _svc = svc;

        /// <summary>
        /// 獲取 KPI 摘要（總營收、總客戶、總訂單等）
        /// </summary>
        [HttpGet("kpi-summary")]
        public async Task<IActionResult> GetKpiSummary([FromQuery] long datasetId)
        {
            if (datasetId <= 0)
                return BadRequest("無效的資料集 ID");

            var data = await _svc.GetKpiSummaryAsync(datasetId,UserId);
            return Ok(data);
        }

        /// <summary>
        /// 獲取年齡分布統計
        /// </summary>
        [HttpGet("age-distribution")]
        public async Task<IActionResult> GetAgeDistribution([FromQuery] long datasetId)
        {
            if (datasetId <= 0)
                return BadRequest("無效的資料集 ID");

            var data = await _svc.GetAgeDistributionAsync(datasetId, UserId);
            return Ok(data);
        }

        /// <summary>
        /// 獲取性別比例
        /// </summary>
        [HttpGet("gender-share")]
        public async Task<IActionResult> GetGenderShare([FromQuery] long datasetId)
        {
            if (datasetId <= 0)
                return BadRequest("無效的資料集 ID");

            var data = await _svc.GetGenderShareAsync(datasetId, UserId);
            return Ok(data);
        }

        /// <summary>
        /// 獲取月營收趨勢
        /// </summary>
        [HttpGet("monthly-revenue-trend")]
        public async Task<IActionResult> GetMonthlyRevenueTrend([FromQuery] long datasetId, [FromQuery] int months = 12)
        {
            if (datasetId <= 0)
                return BadRequest("無效的資料集 ID");

            if (months <= 0 || months > 24)
                return BadRequest("月份數量必須在 1-24 之間");

            var data = await _svc.GetMonthlyRevenueTrendAsync(datasetId, months);
            return Ok(data);
        }

        /// <summary>
        /// 獲取地區分布
        /// </summary>
        [HttpGet("region-distribution")]
        public async Task<IActionResult> GetRegionDistribution([FromQuery] long datasetId)
        {
            if (datasetId <= 0)
                return BadRequest("無效的資料集 ID");

            var data = await _svc.GetRegionDistributionAsync(datasetId, UserId);
            return Ok(data);
        }

        /// <summary>
        /// 獲取產品類別銷量
        /// </summary>
        [HttpGet("product-category-sales")]
        public async Task<IActionResult> GetProductCategorySales([FromQuery] long datasetId)
        {
            if (datasetId <= 0)
                return BadRequest("無效的資料集 ID");

            var data = await _svc.GetProductCategorySalesAsync(datasetId, UserId);
            return Ok(data);
        }

        /// <summary>
        /// 清除指定資料集的快取（管理員功能）
        /// </summary>
        [HttpDelete("cache/{datasetId}")]
        public async Task<IActionResult> ClearCache(long datasetId)
        {
            if (datasetId <= 0)
                return BadRequest("無效的資料集 ID");

            await _svc.RemoveDatasetCacheAsync(datasetId);
            return Ok(new { message = $"已清除資料集 {datasetId} 的快取" });
        }
    }
}