using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LogController : ControllerBase
    {
        private readonly UserLogService _logService;

        public LogController(UserLogService logService)
        {
            _logService = logService;
        }

        // Lấy danh sách toàn bộ hoạt động (Mới nhất lên đầu)
        [HttpGet]
        public async Task<IActionResult> GetAllLogs()
        {
            var logs = await _logService.GetAllLogsAsync();
            return Ok(logs);
        }
    }
}