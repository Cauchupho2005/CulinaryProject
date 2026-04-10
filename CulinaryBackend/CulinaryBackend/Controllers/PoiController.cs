using CulinaryBackend.Models;
using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

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

        // HÀM XỬ LÝ LÕI: Lọc đúng ngôn ngữ và ép kiểu về dạng "phẳng" cho App MAUI
        private object HydrateAndFallback(Poi poi, string lang)
        {
            PoiLocalization content = null;

            // Thuật toán Fallback 3 Tầng: Tìm ngôn ngữ yêu cầu -> Không có thì tìm Tiếng Anh (en) -> Không có nữa thì lấy Tiếng Việt (vi)
            if (poi.Localizations != null)
            {
                if (poi.Localizations.ContainsKey(lang))
                    content = poi.Localizations[lang];
                else if (poi.Localizations.ContainsKey("en"))
                    content = poi.Localizations["en"];
                else if (poi.Localizations.ContainsKey("vi"))
                    content = poi.Localizations["vi"];
            }

            // Trả về cấu trúc json phẳng khớp 100% với file PoiModel.cs của MAUI
            return new
            {
                id = poi.Id,
                title = content?.Title ?? "Đang cập nhật...",
                description = content?.Description ?? "",
                address = content?.Address ?? "",
                coverImageUrl = poi.CoverImageUrl,
                location = poi.Location
            };
        }

        // API 1: Lấy tất cả (App MAUI gọi cái này, ví dụ: GET /api/poi?lang=ja)
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string lang = "vi")
        {
            var dbPois = await _poiService.GetAsync();
            var result = dbPois.Select(p => HydrateAndFallback(p, lang)).ToList();
            return Ok(result);
        }

        // API 2: Tìm quán ăn gần đây
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby(double lng, double lat, double radius = 3000, [FromQuery] string lang = "vi")
        {
            var dbPois = await _poiService.GetNearbyPoisAsync(lng, lat, radius);
            var result = dbPois.Select(p => HydrateAndFallback(p, lang)).ToList();
            return Ok(result);
        }

        // API 3: Thêm món ăn mới (Lát nữa chúng ta dùng để nhét Data test)
        [HttpPost]
        public async Task<IActionResult> Post(Poi newPoi)
        {
            await _poiService.CreateAsync(newPoi);
            // Trả về dữ liệu vừa tạo
            return CreatedAtAction(nameof(Get), new { id = newPoi.Id }, newPoi);
        }
    }
}