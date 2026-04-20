using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace CulinaryBackend.Models
{
    // Bảng lưu lịch sử từng lần quét QR
    public class QrScanLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("poiId")]
        public string PoiId { get; set; } = string.Empty;

        [BsonElement("deviceId")]
        public string DeviceId { get; set; } = string.Empty; // Lưu lại ID máy để phân biệt khách

        [BsonElement("scannedAt")]
        public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    }

    // Class phụ dùng để nhận dữ liệu từ App bắn lên
    public class QrScanRequest
    {
        public string PoiId { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
    }
}