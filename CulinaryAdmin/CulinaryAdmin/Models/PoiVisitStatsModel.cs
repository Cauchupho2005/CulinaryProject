using System.Text.Json.Serialization;

namespace CulinaryAdmin.Models
{
    public class PoiVisitStatsModel
    {
        [JsonPropertyName("poiId")]
        public string PoiId { get; set; } = string.Empty;

        [JsonPropertyName("poiTitle")]
        public string PoiTitle { get; set; } = string.Empty;

        [JsonPropertyName("totalVisits")]
        public int TotalVisits { get; set; }
    }
}
