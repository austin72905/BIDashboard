using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.Interfaces;
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
        public UploadsController(IIngestService svc) => _svc = svc;

        [HttpPost("csv")]
        [DisableRequestSizeLimit]
        [Authorize]
        public async Task<IActionResult> UploadCsv([FromForm] UploadCsvDto req)
        {
            // 從 JWT 中提取 user ID
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("無效的用戶認證");
            }

            var result = await _svc.UploadCsvAsync(req.File, userId);
            return Ok(result);
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
        /// <param name="limit">限制筆數，預設 50</param>
        /// <param name="offset">偏移量，預設 0</param>
        /// <returns>上傳歷史紀錄列表</returns>
        [HttpGet("history")]
        [Authorize]
        public async Task<IActionResult> GetUploadHistory([FromQuery] int limit = 50, [FromQuery] int offset = 0)
        {
            // 從 JWT 中提取 user ID
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("無效的用戶認證");
            }

            var history = await _svc.GetUploadHistoryAsync(userId, limit, offset);
            return Ok(history);
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
    }
}

