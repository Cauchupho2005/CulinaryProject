using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CulinaryBackend.Models
{
    public class Poi
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; } = null!;

        [BsonElement("description")]
        public string Description { get; set; } = null!;

        [BsonElement("address")]
        public string Address { get; set; } = null!;

        [BsonElement("coverImageUrl")]
        public string? CoverImageUrl { get; set; }

        [BsonElement("location")]
        public GeoLocation Location { get; set; } = null!;
    }

    public class GeoLocation
    {
        [BsonElement("type")]
        public string Type { get; set; } = "Point";

        [BsonElement("coordinates")]
        public double[] Coordinates { get; set; } = new double[2];
    }
}