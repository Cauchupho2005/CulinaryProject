using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CulinaryAdmin.Models
{
    public class PoiModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [Required(ErrorMessage = "Bắt buộc phải nhập tên quán!")]
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Bắt buộc nhập mô tả!")]
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Bắt buộc phải nhập địa chỉ!")]
        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("coverImageUrl")]
        public string? CoverImageUrl { get; set; }

        [JsonPropertyName("location")]
        public GeoLocation? Location { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "pending";

        [JsonPropertyName("ownerId")]
        public string? OwnerId { get; set; }

        public string DistanceText { get; set; } = "--- km";

        public double DistanceValue { get; set; }
    }

    public class GeoLocation
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "Point";

        [JsonPropertyName("coordinates")]
        public double[] Coordinates { get; set; } = new double[] { 0, 0 };
    }
}