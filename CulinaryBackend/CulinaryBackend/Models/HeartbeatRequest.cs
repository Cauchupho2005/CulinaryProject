namespace CulinaryBackend.Models
{
    public class HeartbeatRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}