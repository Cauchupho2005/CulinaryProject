using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CulinaryBackend.Models
{
    public class UserLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Username { get; set; } = null!;

        public string Action { get; set; } = null!; // VD: "ĐĂNG NHẬP", "XÓA QUÁN ĂN"

        public string? Details { get; set; } // VD: "Đã xóa quán: Bún Đậu Mắm Tôm"

        public string? DeviceInfo { get; set; } // Thiết bị thực hiện

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}