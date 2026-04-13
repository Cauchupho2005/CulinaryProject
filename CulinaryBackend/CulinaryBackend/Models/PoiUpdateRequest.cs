namespace CulinaryBackend.Models
{
    public class PoiUpdateRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? CoverImageUrl { get; set; }
        public GeoLocation? Location { get; set; }
        public string? Status { get; set; }
        public string? OwnerId { get; set; }
    }
}
