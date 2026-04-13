using CulinaryBackend.Models;
using CulinaryBackend.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IMongoCollection<UserModel> _usersCollection;
        private readonly EmailService _emailService;

        public UserController(IMongoDatabase mongoDatabase, EmailService emailService)
        {
            _usersCollection = mongoDatabase.GetCollection<UserModel>("Users");
            _emailService = emailService;
        }

        private static bool IsLikelyEmail(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains("@");
        }

        // 1. Lấy toàn bộ danh sách User
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _usersCollection.Find(_ => true).ToListAsync();
            return Ok(users);
        }

        // 2. Cập nhật User (đổi role, fullName, isActive, ownerId)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request)
        {
            var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng!" });

            var update = Builders<UserModel>.Update
                .Set(u => u.Role, request.Role)
                .Set(u => u.FullName, request.FullName)
                .Set(u => u.OwnerId, request.OwnerId)
                .Set(u => u.IsActive, request.IsActive);

            await _usersCollection.UpdateOneAsync(u => u.Id == id, update);

            if (user.IsActive != request.IsActive && IsLikelyEmail(user.Username))
            {
                var action = request.IsActive ? "mở khóa" : "khóa";
                await _emailService.SendUserAccountEventEmailAsync(user.Username, user.Username, action);
            }

            return Ok(new { message = "Cập nhật thành công!" });
        }

        // 3. Khoá / mở khoá tài khoản
        [HttpPatch("{id}/toggle-active")]
        public async Task<IActionResult> ToggleActive(string id)
        {
            var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng!" });

            var update = Builders<UserModel>.Update.Set(u => u.IsActive, !user.IsActive);
            await _usersCollection.UpdateOneAsync(u => u.Id == id, update);

            if (IsLikelyEmail(user.Username))
            {
                var action = user.IsActive ? "khóa" : "mở khóa";
                await _emailService.SendUserAccountEventEmailAsync(user.Username, user.Username, action);
            }

            return Ok(new { message = user.IsActive ? "Đã khoá tài khoản!" : "Đã mở khoá tài khoản!", isActive = !user.IsActive });
        }

        // 4. Xoá User
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null) return NotFound(new { message = "Không tìm thấy người dùng!" });

            var result = await _usersCollection.DeleteOneAsync(u => u.Id == id);
            if (result.DeletedCount == 0) return NotFound(new { message = "Không tìm thấy người dùng!" });

            if (IsLikelyEmail(user.Username))
            {
                await _emailService.SendUserAccountEventEmailAsync(user.Username, user.Username, "xóa");
            }

            return Ok(new { message = "Đã xoá tài khoản!" });
        }
    }

    public class UpdateUserRequest
    {
        public string Role { get; set; } = "poi_owner";
        public string? FullName { get; set; }
        public string? OwnerId { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
