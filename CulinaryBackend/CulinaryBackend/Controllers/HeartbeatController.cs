using CulinaryBackend.Models;
using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HeartbeatController : ControllerBase
    {
        private readonly ActiveUserTracker _tracker;

        public HeartbeatController(ActiveUserTracker tracker)
        {
            _tracker = tracker;
        }

        // API 1: Dành cho Mobile App gọi lên mỗi 10 giây
        [HttpPost("ping")]
        public IActionResult Ping([FromBody] HeartbeatRequest request)
        {
            if (string.IsNullOrEmpty(request.DeviceId))
            {
                return BadRequest("DeviceId không được để trống.");
            }

            // Ghi nhận nhịp tim
            _tracker.Ping(request.DeviceId, request.Latitude, request.Longitude);

            return Ok(new { Message = "Heartbeat received" });
        }

        // API 2: Dành cho Web Admin lấy tổng số Active Users hiển thị lên Dashboard
        [HttpGet("active-users")]
        public IActionResult GetActiveUsers()
        {
            int count = _tracker.GetActiveUserCount();
            return Ok(new { ActiveUsers = count });
        }
    }
}