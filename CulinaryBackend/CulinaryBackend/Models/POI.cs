using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace CulinaryBackend.Models
{
    [BsonIgnoreExtraElements] // Bùa chú bỏ qua data rác cũ
    public class Poi
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("coverImageUrl")]
        public string? CoverImageUrl { get; set; }

        [BsonElement("location")]
        public GeoLocation Location { get; set; } = null!;

        [BsonElement("localizations")]
        public Dictionary<string, PoiLocalization> Localizations { get; set; } = new();

        [BsonElement("status")]
        public string Status { get; set; } = "pending"; // pending, approved, rejected

        [BsonElement("ownerId")]
        public string? OwnerId { get; set; } // ID của chủ quán
    }

    public class PoiLocalization
    {
        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("address")]
        public string Address { get; set; } = string.Empty;
    }

    public class GeoLocation
    {
        [BsonElement("type")]
        public string Type { get; set; } = "Point";

        [BsonElement("coordinates")]
        public double[] Coordinates { get; set; } = new double[2];
    }
}