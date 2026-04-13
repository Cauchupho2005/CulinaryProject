using CulinaryBackend.Models;
using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PoiController : ControllerBase
    {
        private readonly PoiService _poiService;
        private readonly EmailService _emailService;
        private readonly IMongoCollection<UserModel> _usersCollection;

        public PoiController(PoiService poiService, EmailService emailService, IMongoDatabase mongoDatabase)
        {
            _poiService = poiService;
            _emailService = emailService;
            _usersCollection = mongoDatabase.GetCollection<UserModel>("Users");
        }

        private static bool IsLikelyEmail(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains("@");
        }

        private static string GetPoiTitle(Poi poi)
        {
            if (poi.Localizations?.ContainsKey("vi") == true)
                return poi.Localizations["vi"].Title;

            return poi.Localizations?.Values.FirstOrDefault()?.Title ?? "POI";
        }

        private async Task<string?> GetOwnerEmailByOwnerIdAsync(string? ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return null;

            var owner = await _usersCollection.Find(u => u.OwnerId == ownerId).FirstOrDefaultAsync();
            if (owner == null || !IsLikelyEmail(owner.Username))
                return null;

            return owner.Username;
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
                title = content?.Title ?? "",
                description = content?.Description ?? "",
                address = content?.Address ?? "",
                coverImageUrl = poi.CoverImageUrl,
                location = poi.Location,
                status = poi.Status ?? "pending",
                ownerId = poi.OwnerId
            };
        }

        // API 1: Lấy tất cả (App MAUI gọi cái này, ví dụ: GET /api/poi?lang=ja)
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string lang = "vi", [FromQuery] bool includeDeleted = false, [FromQuery] string? status = null)
        {
            var dbPois = await _poiService.GetAsync();

            if (!string.IsNullOrWhiteSpace(status))
            {
                dbPois = dbPois.Where(p => p.Status == status).ToList();
            }
            else if (!includeDeleted)
            {
                dbPois = dbPois.Where(p => p.Status == "approved").ToList();
            }

            var result = dbPois.Select(p => HydrateAndFallback(p, lang)).ToList();
            return Ok(result);
        }

        // API 2: Tìm quán ăn gần đây
        [HttpGet("nearby")]
        public async Task<IActionResult> GetNearby(double lng, double lat, double radius = 3000, [FromQuery] string lang = "vi", [FromQuery] bool includeDeleted = false, [FromQuery] string? status = null)
        {
            var dbPois = await _poiService.GetNearbyPoisAsync(lng, lat, radius);

            if (!string.IsNullOrWhiteSpace(status))
            {
                dbPois = dbPois.Where(p => p.Status == status).ToList();
            }
            else if (!includeDeleted)
            {
                dbPois = dbPois.Where(p => p.Status == "approved").ToList();
            }

            var result = dbPois.Select(p => HydrateAndFallback(p, lang)).ToList();
            return Ok(result);
        }

        // API 3: Thêm món ăn mới (Lát nữa chúng ta dùng để nhét Data test)
        [HttpPost]
        public async Task<IActionResult> Post(Poi newPoi)
        {
            if (string.IsNullOrWhiteSpace(newPoi.OwnerId))
            {
                return BadRequest("OwnerId là bắt buộc. Admin không được tạo POI trực tiếp.");
            }

            var vi = newPoi.Localizations?.ContainsKey("vi") == true
                ? newPoi.Localizations["vi"]
                : null;

            if (vi == null
                || string.IsNullOrWhiteSpace(vi.Title)
                || string.IsNullOrWhiteSpace(vi.Address)
                || string.IsNullOrWhiteSpace(vi.Description))
            {
                return BadRequest("Thiếu dữ liệu bắt buộc: title, address, description.");
            }

            newPoi.Status = "pending";
            await _poiService.CreateAsync(newPoi);

            var poiTitle = newPoi.Localizations?.ContainsKey("vi") == true
                ? newPoi.Localizations["vi"].Title
                : "POI mới";
            await _emailService.SendPoiEventEmailAsync(poiTitle, "tạo mới", newPoi.Status);

            var ownerEmail = await GetOwnerEmailByOwnerIdAsync(newPoi.OwnerId);
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                await _emailService.SendCustomEmailAsync(
                    ownerEmail,
                    $"[Culinary] Quán '{poiTitle}' đã được gửi duyệt",
                    $"<p>Quán <strong>{poiTitle}</strong> vừa được tạo và đang ở trạng thái <strong>{newPoi.Status}</strong>.</p>");
            }

            // Trả về dữ liệu vừa tạo
            return CreatedAtAction(nameof(Get), new { id = newPoi.Id }, newPoi);
        }

        // API 4: Cập nhật POI (Admin dùng)
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(string id, [FromBody] JsonElement requestData)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();

            // Parse flat data from Admin
            var title = requestData.GetProperty("title").GetString();
            var description = requestData.TryGetProperty("description", out var descProp) ? descProp.GetString() : "";
            var address = requestData.TryGetProperty("address", out var addrProp) ? addrProp.GetString() : "";
            var coverImageUrl = requestData.TryGetProperty("coverImageUrl", out var imgProp) ? imgProp.GetString() : "";

            // Preserve existing localizations and update vi
            existing.CoverImageUrl = coverImageUrl;

            if (existing.Localizations == null)
                existing.Localizations = new Dictionary<string, PoiLocalization>();

            if (!existing.Localizations.ContainsKey("vi"))
                existing.Localizations["vi"] = new PoiLocalization();

            existing.Localizations["vi"].Title = title ?? "";
            existing.Localizations["vi"].Description = description ?? "";
            existing.Localizations["vi"].Address = address ?? "";

            // Update location if provided
            if (requestData.TryGetProperty("location", out var locationElement))
            {
                var coords = locationElement.GetProperty("coordinates");
                existing.Location = new GeoLocation
                {
                    Type = "Point",
                    Coordinates = new double[] { coords[0].GetDouble(), coords[1].GetDouble() }
                };
            }

            await _poiService.UpdateAsync(id, existing);
            await _emailService.SendPoiEventEmailAsync(existing.Localizations["vi"].Title, "cập nhật", existing.Status);

            var ownerEmail = await GetOwnerEmailByOwnerIdAsync(existing.OwnerId);
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                var poiTitle = GetPoiTitle(existing);
                await _emailService.SendCustomEmailAsync(
                    ownerEmail,
                    $"[Culinary] Quán '{poiTitle}' đã được cập nhật",
                    $"<p>Thông tin quán <strong>{poiTitle}</strong> vừa được cập nhật trên hệ thống.</p><p>Trạng thái hiện tại: <strong>{existing.Status}</strong></p>");
            }
            return NoContent();
        }

        // API 5: Xoá POI (status = deleted)

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            await _poiService.UpdateStatusAsync(id, "deleted");
            var poiTitle = GetPoiTitle(existing);
            await _emailService.SendPoiEventEmailAsync(poiTitle, "xóa", "deleted");

            var ownerEmail = await GetOwnerEmailByOwnerIdAsync(existing.OwnerId);
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                await _emailService.SendCustomEmailAsync(
                    ownerEmail,
                    $"[Culinary] Quán '{poiTitle}' đã bị xóa",
                    $"<p>Quán <strong>{poiTitle}</strong> vừa bị chuyển sang trạng thái <strong>deleted</strong>.</p>");
            }
            return NoContent();
        }

        // API 5.1: Khôi phục POI đã xoá

        [HttpPatch("{id}/restore")]
        public async Task<IActionResult> Restore(string id)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            await _poiService.UpdateStatusAsync(id, "pending");
            var poiTitle = GetPoiTitle(existing);
            await _emailService.SendPoiEventEmailAsync(poiTitle, "khôi phục", "pending");

            var ownerEmail = await GetOwnerEmailByOwnerIdAsync(existing.OwnerId);
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                await _emailService.SendCustomEmailAsync(
                    ownerEmail,
                    $"[Culinary] Quán '{poiTitle}' đã được khôi phục",
                    $"<p>Quán <strong>{poiTitle}</strong> đã được khôi phục về trạng thái <strong>pending</strong>.</p>");
            }
            return NoContent();
        }

        // API 6: Duyệt POI (Admin dùng)
        [HttpPatch("{id}/approve")]
        public async Task<IActionResult> Approve(string id)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            await _poiService.UpdateStatusAsync(id, "approved");
            var poiTitle = GetPoiTitle(existing);
            await _emailService.SendPoiEventEmailAsync(poiTitle, "duyệt", "approved");

            var ownerEmail = await GetOwnerEmailByOwnerIdAsync(existing.OwnerId);
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                await _emailService.SendApprovalEmailAsync(ownerEmail, poiTitle, true);
            }
            return NoContent();
        }

        // API 7: Từ chối POI (Admin dùng)
        [HttpPatch("{id}/reject")]
        public async Task<IActionResult> Reject(string id)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();
            await _poiService.UpdateStatusAsync(id, "rejected");
            var poiTitle = GetPoiTitle(existing);
            await _emailService.SendPoiEventEmailAsync(poiTitle, "từ chối", "rejected");

            var ownerEmail = await GetOwnerEmailByOwnerIdAsync(existing.OwnerId);
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                await _emailService.SendApprovalEmailAsync(ownerEmail, poiTitle, false);
            }
            return NoContent();
        }
    }
}