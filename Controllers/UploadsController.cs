using BIDashboardBackend.DTOs.Request;
using BIDashboardBackend.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> UploadCsv([FromForm] UploadCsvDto req)
        {
            var result = await _svc.UploadCsvAsync(req.File);
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
    }
}

