using System.Text.Json.Serialization;

namespace CulinaryApp.Models
{
    public class PoiModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("coverImageUrl")]
        public string? CoverImageUrl { get; set; }

        [JsonPropertyName("location")]
        public GeoLocation? Location { get; set; }

        public string DistanceText { get; set; } = "--- km";

        [JsonPropertyName("rank")]
        public int Rank { get; set; }

        public double DistanceValue { get; set; }
    }

    public class GeoLocation
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "Point";

        [JsonPropertyName("coordinates")]
        public double[] Coordinates { get; set; } = Array.Empty<double>();
    }
}