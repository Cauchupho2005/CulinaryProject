using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CulinaryBackend.Models
{
    public class PoiVisit
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("poiId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string PoiId { get; set; } = string.Empty;

        [BsonElement("visitedAt")]
        public DateTime VisitedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("userAgent")]
        public string? UserAgent { get; set; }
    }

    public class PoiVisitStats
    {
        public string PoiId { get; set; } = string.Empty;
        public string PoiTitle { get; set; } = string.Empty;
        public int TotalVisits { get; set; }
    }
}
