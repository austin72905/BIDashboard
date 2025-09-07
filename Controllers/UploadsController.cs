using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.Interfaces;
using BIDashboardBackend.Caching;
using BIDashboardBackend.Features.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BIDashboardBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadsController : ControllerBase
    {
        private readonly IIngestService _svc;
        private readonly CacheKeyBuilder _keyBuilder;
        private readonly ICacheService _cache;
        private readonly IBackgroundJobClient _jobs;
        
        public UploadsController(IIngestService svc, CacheKeyBuilder keyBuilder, ICacheService cache, IBackgroundJobClient jobs)
        {
            _svc = svc;
            _keyBuilder = keyBuilder;
            _cache = cache;
            _jobs = jobs;
        }

        /// <summary>
        /// 創建新的資料集
        /// </summary>
        /// <param name="request">創建資料集請求</param>
        /// <returns>創建結果</returns>
        [HttpPost("dataset")]
        [Authorize]
        public async Task<IActionResult> CreateDataset([FromBody] CreateDatasetDto request)
        {
            // 從 JWT 中提取 user ID
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("無效的用戶認證");
            }

            try
            {
                var result = await _svc.CreateDatasetAsync(request.Name, userId, request.Description);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("csv")]
        [DisableRequestSizeLimit]
        [Authorize]
        public async Task<IActionResult> UploadCsv([FromForm] UploadCsvDto req, [FromQuery] long datasetId)
        {
            // 從 JWT 中提取 user ID
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("無效的用戶認證");
            }

            try
            {
                var result = await _svc.UploadCsvAsync(req.File, userId, datasetId);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("mappings")]
        public async Task<IActionResult> UpsertMappings([FromBody] UpsertMappingsRequestDto body)
        {
            await _svc.UpsertMappingsAsync(body);
            return NoContent();
        }

        [HttpGet("{batchId}/columns")]
        public async Task<IActionResult> GetColumns(long batchId)
        {
            var cols = await _svc.GetColumnsAsync(batchId);
            return Ok(cols);
        }

        [HttpGet("{batchId}/mapping-info")]
        public async Task<IActionResult> GetColumnMappingInfo(long batchId)
        {
            var info = await _svc.GetColumnMappingInfoAsync(batchId);
            return Ok(info);
        }

        /// <summary>
        /// 獲取用戶的上傳歷史紀錄
        /// </summary>
        /// <param name="datasetId">資料集 ID</param>
        /// <param name="limit">限制筆數，預設 50</param>
        /// <param name="offset">偏移量，預設 0</param>
        /// <returns>上傳歷史紀錄列表</returns>
        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> GetUploadHistory([FromQuery] long datasetId, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            // 從 JWT 中提取 user ID
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("無效的用戶認證");
            }

            try
            {
                var history = await _svc.GetUploadHistoryAsync(userId, datasetId, limit, offset);
                return Ok(history);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// 獲取指定批次的詳細資訊（包含欄位和映射）
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <returns>批次詳細資訊</returns>
        [HttpGet("{batchId}/details")]
        [Authorize]
        public async Task<IActionResult> GetBatchDetails(long batchId)
        {
            // 從 JWT 中提取 user ID
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("無效的用戶認證");
            }

            var details = await _svc.GetBatchDetailsAsync(batchId, userId);
            if (details == null)
            {
                return NotFound($"找不到批次 ID: {batchId}");
            }

            return Ok(details);
        }

        /// <summary>
        /// 刪除指定的批次
        /// </summary>
        /// <param name="batchId">批次 ID</param>
        /// <returns>刪除結果</returns>
        [HttpDelete("{batchId}")]
        [Authorize]
        public async Task<IActionResult> DeleteBatch(long batchId)
        {
            // 從 JWT 中提取 user ID
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("無效的用戶認證");
            }

            try
            {
                var (success, datasetId) = await _svc.DeleteBatchAsync(batchId, userId);
                if (!success || datasetId == null)
                {
                    return NotFound($"找不到批次 ID: {batchId} 或您沒有權限刪除該批次");
                }

                // 刪除成功後，排程ETL Job重新計算該資料集的指標
                try
                {
                    // 使用Hangfire排程ETL Job來重新計算materialized_metrics
                    // 這裡使用一個特殊的batchId (-1) 來表示重新計算整個資料集
                    _jobs.Enqueue<IEtlJob>(j => j.ProcessBatch(datasetId.Value, -1));
                }
                catch (Exception jobEx)
                {
                    // ETL Job排程失敗不應該影響刪除操作的成功
                    // 記錄錯誤但不拋出異常
                    Console.WriteLine($"排程ETL Job失敗: {jobEx.Message}");
                }

                return Ok(new { message = "批次刪除成功，正在重新計算指標", batchId, datasetId });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}

