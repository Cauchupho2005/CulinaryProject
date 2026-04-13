using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PoiVisitController : ControllerBase
    {
        private readonly PoiVisitService _visitService;

        public PoiVisitController(PoiVisitService visitService)
        {
            _visitService = visitService;
        }

        [HttpPost("{poiId}")]
        public async Task<IActionResult> TrackVisit(string poiId)
        {
            var userAgent = Request.Headers["User-Agent"].ToString();
            await _visitService.TrackVisitAsync(poiId, userAgent);
            return Ok();
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _visitService.GetVisitStatsAsync();
            return Ok(stats);
        }

        [HttpGet("{poiId}/count")]
        public async Task<IActionResult> GetPoiVisitCount(string poiId)
        {
            var count = await _visitService.GetPoiVisitCountAsync(poiId);
            return Ok(new { poiId, visitCount = count });
        }
    }
}
