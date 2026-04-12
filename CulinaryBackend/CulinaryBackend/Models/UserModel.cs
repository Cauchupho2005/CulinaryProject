using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace CulinaryBackend.Models
{
    public class UserModel
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [BsonElement("username")]
        [JsonPropertyName("username")]
        public string Username { get; set; } = null!;

        [BsonElement("passwordHash")]
        [JsonIgnore] // Rất quan trọng: Không bao giờ trả về PasswordHash qua API
        public string PasswordHash { get; set; } = null!;

        [BsonElement("role")]
        [JsonPropertyName("role")]
        // Các giá trị Role: "super_admin", "admin", "poi_owner"
        public string Role { get; set; } = "poi_owner";

        [BsonElement("ownerId")]
        [JsonPropertyName("ownerId")]
        // Nếu là admin thì trường này null. Nếu là chủ quán, trường này sẽ là ID của họ để đối chiếu với các quán ăn.
        public string? OwnerId { get; set; }

        [BsonElement("fullName")]
        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [BsonElement("isActive")]
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true; // Dùng để khóa tài khoản khi cần

        [BsonElement("createdAt")]
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}