using System.ComponentModel.DataAnnotations;

namespace CulinaryBackend.Models
{
    /// <summary>
    /// Model phẳng dành riêng cho việc tạo POI mới từ Swagger/Client
    /// Giúp giao diện API sạch sẽ và dễ dùng hơn
    /// </summary>
    public class PoiCreateRequest
    {
        [Required(ErrorMessage = "Tên quán (Title) là bắt buộc")]
        public string? Title { get; set; }

        [Required(ErrorMessage = "Mô tả (Description) là bắt buộc")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Địa chỉ (Address) là bắt buộc")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "OwnerId là bắt buộc")]
        public string? OwnerId { get; set; }

        public string? CoverImageUrl { get; set; }

        public GeoLocation? Location { get; set; }
    }
}
