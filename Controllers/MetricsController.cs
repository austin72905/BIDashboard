using BIDashboardBackend.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BIDashboardBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController : ControllerBase
    {
        private readonly IMetricService _svc;
        public MetricsController(IMetricService svc) => _svc = svc;


        [HttpGet("age-distribution")]
        public async Task<IActionResult> GetAgeDistribution([FromQuery] long datasetId)
        {
            var data = await _svc.GetAgeDistributionAsync(datasetId);
            return Ok(data);
        }


        [HttpGet("gender-share")]
        public async Task<IActionResult> GetGenderShare([FromQuery] long datasetId)
        {
            var data = await _svc.GetGenderShareAsync(datasetId);
            return Ok(data);
        }
    }
}
