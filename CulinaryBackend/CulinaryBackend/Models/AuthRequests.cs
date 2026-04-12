using System.Text.Json.Serialization;

namespace CulinaryBackend.Models
{
    // Class dùng để hứng dữ liệu khi tạo tài khoản
    public class RegisterRequest
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = null!;

        [JsonPropertyName("password")]
        public string Password { get; set; } = null!;

        [JsonPropertyName("role")]
        public string Role { get; set; } = "poi_owner"; // Mặc định là chủ quán

        [JsonPropertyName("ownerId")]
        public string? OwnerId { get; set; }

        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }
    }

    // Class dùng để hứng dữ liệu khi đăng nhập
    public class LoginRequest
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = null!;

        [JsonPropertyName("password")]
        public string Password { get; set; } = null!;
    }

    // Class dùng để trả về thông tin user (đã giấu password)
    public class AuthResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("ownerId")]
        public string? OwnerId { get; set; }

        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }
    }
}