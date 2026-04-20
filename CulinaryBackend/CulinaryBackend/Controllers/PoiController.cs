using CulinaryBackend.Models;
using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using System.Linq;
using System.Threading.Tasks;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PoiController : ControllerBase
    {
        private readonly PoiService _poiService;
        private readonly EmailService _emailService;
        private readonly IMongoCollection<UserModel> _usersCollection;
        private readonly UserLogService _userLogService;
        private readonly PoiVisitService _poiVisitService; // Service để ghi nhận lượt truy cập/quét
        private readonly IMemoryCache _cache; // Để chống spam 1 phút

        public PoiController(
            PoiService poiService,
            EmailService emailService,
            IMongoDatabase mongoDatabase,
            UserLogService userLogService,
            PoiVisitService poiVisitService,
            IMemoryCache cache)
        {
            _poiService = poiService;
            _emailService = emailService;
            _usersCollection = mongoDatabase.GetCollection<UserModel>("Users");
            _userLogService = userLogService;
            _poiVisitService = poiVisitService;
            _cache = cache;
        }

        // ================= API MỚI: QUÉT QR FALLBACK =================
        [HttpGet("{id}/scan-fallback")]
        public async Task<IActionResult> ScanFallback(string id, [FromQuery] string deviceId, [FromQuery] string lang = "vi")
        {
            var poi = await _poiService.GetByIdAsync(id);
            if (poi == null) return NotFound(new { message = "Không tìm thấy địa điểm" });

            // 1. Lấy nội dung thuyết minh theo ngôn ngữ
            string ttsText = "Nội dung thuyết minh đang được cập nhật.";
            if (poi.Localizations != null && poi.Localizations.ContainsKey(lang))
            {
                ttsText = poi.Localizations[lang].Description;
            }

            // 2. Chống Spam 1 phút & Ghi nhận lượt quét
            if (!string.IsNullOrEmpty(deviceId))
            {
                string cacheKey = $"QR_Spam_{deviceId}_{id}";
                if (!_cache.TryGetValue(cacheKey, out _))
                {
                    // A. Đặt khóa chặn 1 phút
                    _cache.Set(cacheKey, true, TimeSpan.FromMinutes(1));

                    // B. Ghi nhận lượt quét vào bảng PoiVisits (để hiện lên Dashboard Owner)
                    // Chúng ta đánh dấu User-Agent là "QR_Web_Fallback" để phân biệt với App
                    await _poiVisitService.TrackVisitAsync(id, "QR_Web_Fallback");

                    // C. Ghi log hệ thống
                    await _userLogService.LogActionAsync("Khách vãng lai", "QUÉT QR WEB",
                        $"Thiết bị {deviceId} quét mã quán: {GetPoiTitle(poi)}", "Browser");
                }
            }

            return Ok(new
            {
                poiId = id,
                title = GetPoiTitle(poi),
                description = ttsText,
                lat = poi.Location.Coordinates[1], // Vĩ độ
                lng = poi.Location.Coordinates[0]  // Kinh độ
            });
        }

        // ================= CÁC API CŨ CỦA BẠN (GIỮ NGUYÊN) =================
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string lang = "vi")
        {
            var pois = await _poiService.GetAsync();
            return Ok(pois);
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lon, [FromQuery] double lat, [FromQuery] double dist = 1000)
        {
            var pois = await _poiService.GetNearbyPoisAsync(lon, lat, dist);
            return Ok(pois);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var poi = await _poiService.GetByIdAsync(id);
            if (poi == null) return NotFound();
            return Ok(poi);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PoiCreateRequest request)
        {
            var newPoi = new Poi
            {
                CoverImageUrl = request.CoverImageUrl,
                Location = request.Location!,
                OwnerId = request.OwnerId,
                Status = "pending",
                Localizations = new Dictionary<string, PoiLocalization>
                {
                    { "vi", new PoiLocalization { Title = request.Title!, Description = request.Description!, Address = request.Address! } }
                }
            };
            await _poiService.CreateAsync(newPoi);
            await _userLogService.LogActionAsync(request.OwnerId ?? "Chủ quán", "TẠO QUÁN ĂN", $"Tạo quán: {request.Title}");
            return Ok(newPoi);
        }

        [HttpPatch("{id}/approve")]
        public async Task<IActionResult> Approve(string id)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            await _poiService.UpdateStatusAsync(id, "approved");
            var poiTitle = GetPoiTitle(existing);
            await _emailService.SendPoiEventEmailAsync(poiTitle, "duyệt", "approved");
            await _userLogService.LogActionAsync("Admin", "DUYỆT QUÁN ĂN", $"Đã duyệt quán: {poiTitle}");
            return NoContent();
        }

        [HttpPatch("{id}/reject")]
        public async Task<IActionResult> Reject(string id)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            await _poiService.UpdateStatusAsync(id, "rejected");
            var poiTitle = GetPoiTitle(existing);
            await _emailService.SendPoiEventEmailAsync(poiTitle, "từ chối", "rejected");
            await _userLogService.LogActionAsync("Admin", "TỪ CHỐI QUÁN ĂN", $"Đã từ chối quán: {poiTitle}");
            return NoContent();
        }

        private static string GetPoiTitle(Poi poi)
        {
            if (poi.Localizations?.ContainsKey("vi") == true)
                return poi.Localizations["vi"].Title;
            return poi.Localizations?.Values.FirstOrDefault()?.Title ?? "Không tên";
        }
    }
}