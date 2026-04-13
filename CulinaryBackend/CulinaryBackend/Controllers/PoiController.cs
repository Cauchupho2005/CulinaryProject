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
        private readonly UserLogService _userLogService; // Đã thêm Log Service

        public PoiController(PoiService poiService, EmailService emailService, IMongoDatabase mongoDatabase, UserLogService userLogService)
        {
            _poiService = poiService;
            _emailService = emailService;
            _usersCollection = mongoDatabase.GetCollection<UserModel>("Users");
            _userLogService = userLogService;
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

        private object HydrateAndFallback(Poi poi, string lang)
        {
            PoiLocalization content = null;

            if (poi.Localizations != null)
            {
                if (poi.Localizations.ContainsKey(lang))
                    content = poi.Localizations[lang];
                else if (poi.Localizations.ContainsKey("en"))
                    content = poi.Localizations["en"];
                else if (poi.Localizations.ContainsKey("vi"))
                    content = poi.Localizations["vi"];
            }

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

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] PoiCreateRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var newPoi = new Poi
            {
                OwnerId = request.OwnerId,
                CoverImageUrl = request.CoverImageUrl,
                Status = "pending",
                Localizations = new Dictionary<string, PoiLocalization>
                {
                    ["vi"] = new PoiLocalization
                    {
                        Title = request.Title,
                        Description = request.Description,
                        Address = request.Address
                    }
                },
                Location = request.Location ?? new GeoLocation { Type = "Point", Coordinates = new double[] { 0, 0 } }
            };

            await _poiService.CreateAsync(newPoi);

            // Gửi email ngầm để không làm chậm API
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendPoiEventEmailAsync(request.Title, "tạo mới", newPoi.Status);
                    var ownerEmail = await GetOwnerEmailByOwnerIdAsync(newPoi.OwnerId);
                    if (!string.IsNullOrWhiteSpace(ownerEmail))
                    {
                        await _emailService.SendCustomEmailAsync(
                            ownerEmail,
                            $"[Culinary] Quán '{request.Title}' đã được gửi duyệt",
                            $"<p>Quán <strong>{request.Title}</strong> vừa được tạo và đang ở trạng thái <strong>{newPoi.Status}</strong>.</p>");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background email error: {ex.Message}");
                }
            });

            // GHI LOG
            var currentUser = User.Identity?.Name ?? newPoi.OwnerId;
            await _userLogService.LogActionAsync(currentUser, "TẠO QUÁN ĂN", $"Đã tạo quán mới: {request.Title}");

            return CreatedAtAction(nameof(Get), new { id = newPoi.Id }, newPoi);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(string id, [FromBody] JsonElement requestData)
        {
            var existing = await _poiService.GetByIdAsync(id);
            if (existing == null) return NotFound();

            var title = requestData.GetProperty("title").GetString();
            var description = requestData.TryGetProperty("description", out var descProp) ? descProp.GetString() : "";
            var address = requestData.TryGetProperty("address", out var addrProp) ? addrProp.GetString() : "";
            var coverImageUrl = requestData.TryGetProperty("coverImageUrl", out var imgProp) ? imgProp.GetString() : "";

            existing.CoverImageUrl = coverImageUrl;

            if (existing.Localizations == null)
                existing.Localizations = new Dictionary<string, PoiLocalization>();

            if (!existing.Localizations.ContainsKey("vi"))
                existing.Localizations["vi"] = new PoiLocalization();

            existing.Localizations["vi"].Title = title ?? "";
            existing.Localizations["vi"].Description = description ?? "";
            existing.Localizations["vi"].Address = address ?? "";

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

            // GHI LOG
            var currentUser = User.Identity?.Name ?? "Hệ thống";
            await _userLogService.LogActionAsync(currentUser, "CẬP NHẬT QUÁN ĂN", $"Đã sửa thông tin quán: {GetPoiTitle(existing)}");

            return NoContent();
        }

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

            // GHI LOG
            var currentUser = User.Identity?.Name ?? "Hệ thống";
            await _userLogService.LogActionAsync(currentUser, "XÓA QUÁN ĂN", $"Đã chuyển quán '{poiTitle}' sang thùng rác");

            return NoContent();
        }

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

            // GHI LOG
            var currentUser = User.Identity?.Name ?? "Hệ thống";
            await _userLogService.LogActionAsync(currentUser, "KHÔI PHỤC QUÁN ĂN", $"Đã khôi phục quán '{poiTitle}' về trạng thái chờ duyệt");

            return NoContent();
        }

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

            // GHI LOG
            var currentUser = User.Identity?.Name ?? "Hệ thống";
            await _userLogService.LogActionAsync(currentUser, "DUYỆT QUÁN ĂN", $"Đã duyệt cho phép quán '{poiTitle}' hoạt động");

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

            var ownerEmail = await GetOwnerEmailByOwnerIdAsync(existing.OwnerId);
            if (!string.IsNullOrWhiteSpace(ownerEmail))
            {
                await _emailService.SendApprovalEmailAsync(ownerEmail, poiTitle, false);
            }

            // GHI LOG
            var currentUser = User.Identity?.Name ?? "Hệ thống";
            await _userLogService.LogActionAsync(currentUser, "TỪ CHỐI QUÁN ĂN", $"Đã từ chối cấp phép cho quán '{poiTitle}'");

            return NoContent();
        }
    }
}