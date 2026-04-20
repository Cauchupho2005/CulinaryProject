using CulinaryBackend.Models;
using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PoiController : ControllerBase
    {
        private readonly PoiService _poiService;
        private readonly EmailService _emailService;
        private readonly UserLogService _userLogService;

        public PoiController(PoiService poiService, EmailService emailService, UserLogService userLogService)
        {
            _poiService = poiService;
            _emailService = emailService;
            _userLogService = userLogService;
        }

        // =========================================================================
        // HÀM HELPER: "Đập phẳng" dữ liệu đa ngôn ngữ thành dữ liệu phẳng cho App/Admin
        // =========================================================================
        private PoiResponseDto MapToFlatDto(Poi poi, string lang = "vi")
        {
            var dto = new PoiResponseDto
            {
                Id = poi.Id,
                CoverImageUrl = poi.CoverImageUrl,
                Status = poi.Status,
                OwnerId = poi.OwnerId,
                Location = poi.Location != null ? new GeoLocationDto { Type = poi.Location.Type, Coordinates = poi.Location.Coordinates } : null
            };

            // Ưu tiên lấy ngôn ngữ được yêu cầu, nếu không có thì lấy ngôn ngữ đầu tiên, nếu trống thì để rỗng
            if (poi.Localizations != null && poi.Localizations.ContainsKey(lang))
            {
                dto.Title = poi.Localizations[lang].Title;
                dto.Description = poi.Localizations[lang].Description;
                dto.Address = poi.Localizations[lang].Address;
            }
            else if (poi.Localizations != null && poi.Localizations.Count > 0)
            {
                var firstLang = poi.Localizations.First().Value;
                dto.Title = firstLang.Title;
                dto.Description = firstLang.Description;
                dto.Address = firstLang.Address;
            }
            else
            {
                dto.Title = "Không có tên";
                dto.Description = "Không có mô tả";
                dto.Address = "Không có địa chỉ";
            }

            return dto;
        }

        // =========================================================================
        // CÁC API TRẢ DỮ LIỆU VỀ (Đã sửa để trả về DTO phẳng)
        // =========================================================================

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string lang = "vi")
        {
            var pois = await _poiService.GetAsync();
            // Trả về danh sách đã được đập phẳng
            var result = pois.Select(p => MapToFlatDto(p, lang)).ToList();
            return Ok(result);
        }

        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby([FromQuery] double lon, [FromQuery] double lat, [FromQuery] double dist = 1000, [FromQuery] string lang = "vi")
        {
            var pois = await _poiService.GetNearbyPoisAsync(lon, lat, dist);
            var result = pois.Select(p => MapToFlatDto(p, lang)).ToList();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id, [FromQuery] string lang = "vi")
        {
            var poi = await _poiService.GetByIdAsync(id);
            if (poi == null) return NotFound();
            return Ok(MapToFlatDto(poi, lang));
        }

        // =========================================================================
        // CÁC API TẠO VÀ CẬP NHẬT (Xử lý ngược: Chuyển phẳng thành đa ngôn ngữ để lưu)
        // =========================================================================

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
                    // Mặc định lưu vào tiếng Việt khi tạo mới
                    { "vi", new PoiLocalization { Title = request.Title ?? "", Description = request.Description ?? "", Address = request.Address ?? "" } }
                }
            };
            await _poiService.CreateAsync(newPoi);
            await _userLogService.LogActionAsync(request.OwnerId ?? "Chủ quán", "TẠO QUÁN ĂN", $"Tạo quán: {request.Title}");
            return Ok(MapToFlatDto(newPoi)); // Trả về DTO phẳng
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] PoiUpdateRequest request)
        {
            var existingPoi = await _poiService.GetByIdAsync(id);
            if (existingPoi == null) return NotFound();

            existingPoi.CoverImageUrl = request.CoverImageUrl ?? existingPoi.CoverImageUrl;
            existingPoi.OwnerId = request.OwnerId ?? existingPoi.OwnerId;
            existingPoi.Status = request.Status ?? existingPoi.Status;

            if (request.Location != null)
            {
                existingPoi.Location = request.Location;
            }

            // Cập nhật phần tiếng Việt
            if (existingPoi.Localizations == null) existingPoi.Localizations = new Dictionary<string, PoiLocalization>();
            if (!existingPoi.Localizations.ContainsKey("vi")) existingPoi.Localizations["vi"] = new PoiLocalization();

            existingPoi.Localizations["vi"].Title = request.Title ?? existingPoi.Localizations["vi"].Title;
            existingPoi.Localizations["vi"].Description = request.Description ?? existingPoi.Localizations["vi"].Description;
            existingPoi.Localizations["vi"].Address = request.Address ?? existingPoi.Localizations["vi"].Address;

            await _poiService.UpdateAsync(id, existingPoi);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var poi = await _poiService.GetByIdAsync(id);
            if (poi == null) return NotFound();
            await _poiService.DeleteAsync(id);
            return NoContent();
        }

        [HttpPatch("{id}/approve")]
        public async Task<IActionResult> Approve(string id)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            await _poiService.UpdateStatusAsync(id, "approved");

            string title = MapToFlatDto(existing).Title;
            await _emailService.SendPoiEventEmailAsync(title, "duyệt", "approved");
            await _userLogService.LogActionAsync("Admin", "DUYỆT QUÁN ĂN", $"Đã duyệt quán: {title}");
            return NoContent();
        }

        [HttpPatch("{id}/reject")]
        public async Task<IActionResult> Reject(string id)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            await _poiService.UpdateStatusAsync(id, "rejected");

            string title = MapToFlatDto(existing).Title;
            await _emailService.SendPoiEventEmailAsync(title, "từ chối", "rejected");
            await _userLogService.LogActionAsync("Admin", "TỪ CHỐI QUÁN ĂN", $"Đã từ chối quán: {title}");
            return NoContent();
        }
    }

    // =========================================================================
    // CÁC LỚP DTO (Data Transfer Object)
    // Các lớp này định nghĩa cấu trúc dữ liệu phẳng sẽ được gửi cho App và Admin
    // =========================================================================
    public class PoiResponseDto
    {
        public string? Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public GeoLocationDto? Location { get; set; }
        public string Status { get; set; } = "pending";
        public string? OwnerId { get; set; }
    }

    public class GeoLocationDto
    {
        public string Type { get; set; } = "Point";
        public double[] Coordinates { get; set; } = new double[] { 0, 0 };
    }
}