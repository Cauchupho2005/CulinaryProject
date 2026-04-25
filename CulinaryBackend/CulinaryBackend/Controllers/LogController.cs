using CulinaryBackend.Models;
using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogController : ControllerBase
    {
        private readonly UserLogService _logService;
        private readonly QrScanLogService _qrScanService; // Thêm Service xử lý QR

        // Tiêm (Inject) Service vào Controller
        public LogController(UserLogService logService, QrScanLogService qrScanService)
        {
            _logService = logService;
            _qrScanService = qrScanService;
        }

        // Lấy danh sách toàn bộ hoạt động (Mới nhất lên đầu)
        [HttpGet]
        public async Task<IActionResult> GetAllLogs()
        {
            var logs = await _logService.GetAllLogsAsync();
            return Ok(logs);
        }

        // =========================================================
        // TÍNH NĂNG MỚI: API hứng dữ liệu ghi log khi có người quét QR
        // Link gọi API: POST /api/Log/qr-scan
        // =========================================================
        [HttpPost("qr-scan")]
        public async Task<IActionResult> LogQrScan([FromBody] QrScanRequest request)
        {
            // Kiểm tra xem dữ liệu App gửi lên có bị thiếu không
            if (string.IsNullOrEmpty(request.PoiId) || string.IsNullOrEmpty(request.DeviceId))
            {
                return BadRequest(new { message = "PoiId và DeviceId không được để trống." });
            }

            // Gọi service lưu vào Database
            await _qrScanService.LogScanAsync(request.PoiId, request.DeviceId);

            return Ok(new { message = "Ghi log QR thành công!" });
        }
    }
}