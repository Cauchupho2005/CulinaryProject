using CulinaryBackend.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using BCrypt.Net;

namespace CulinaryBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMongoCollection<UserModel> _usersCollection;

        public AuthController(IMongoDatabase mongoDatabase)
        {
            // Kết nối vào collection 'Users' trong MongoDB
            _usersCollection = mongoDatabase.GetCollection<UserModel>("Users");
        }

        // 1. API ĐĂNG KÝ (TẠO TÀI KHOẢN)
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Kiểm tra xem Username đã tồn tại chưa
            var existingUser = await _usersCollection.Find(u => u.Username == request.Username).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                return BadRequest(new { message = "Tên đăng nhập đã tồn tại!" });
            }

            // Tạo User mới và BĂM MẬT KHẨU
            var newUser = new UserModel
            {
                Username = request.Username,
                // Dùng BCrypt để mã hóa mật khẩu 1 chiều
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = request.Role,
                OwnerId = request.OwnerId,
                FullName = request.FullName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _usersCollection.InsertOneAsync(newUser);

            return Ok(new { message = "Đăng ký tài khoản thành công!" });
        }

        // 2. API ĐĂNG NHẬP
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            // Tìm user theo username
            var user = await _usersCollection.Find(u => u.Username == request.Username).FirstOrDefaultAsync();

            // Nếu không tìm thấy, hoặc tài khoản đã bị khóa
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu!" });
            }

            // KIỂM TRA MẬT KHẨU (So sánh password gửi lên với mã Hash trong DB)
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu!" });
            }

            // Nếu đúng mật khẩu, trả về thông tin User (dùng AuthResponse để giấu PasswordHash)
            var responseData = new AuthResponse
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role,
                OwnerId = user.OwnerId,
                FullName = user.FullName
            };

            return Ok(responseData);
        }
    }
}