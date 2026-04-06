using CulinaryBackend.Models;
using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PoiController : ControllerBase
    {
        private readonly PoiService _poiService;

        public PoiController(PoiService poiService)
        {
            _poiService = poiService;
        }

        // API: Lấy tất cả (GET: /api/poi)
        [HttpGet]
        public async Task<List<Poi>> Get() => await _poiService.GetAsync();

        // API: Tìm quán ăn gần đây (GET: /api/poi/nearby?lng=106.70&lat=10.76&radius=2000)
        [HttpGet("nearby")]
        public async Task<List<Poi>> GetNearby(double lng, double lat, double radius = 3000)
        {
            // Mặc định tìm trong bán kính 3km (3000 mét)
            return await _poiService.GetNearbyPoisAsync(lng, lat, radius);
        }

        // API: Thêm món ăn mới (POST: /api/poi)
        [HttpPost]
        public async Task<IActionResult> Post(Poi newPoi)
        {
            await _poiService.CreateAsync(newPoi);
            return CreatedAtAction(nameof(Get), new { id = newPoi.Id }, newPoi);
        }
    }
}