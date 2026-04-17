using System.Text;
using System.Text.Json;
using CulinaryApp.Helpers;
using Microsoft.Maui.Devices.Sensors; // Cần thêm thư viện này cho Geolocation

namespace CulinaryApp.Services
{
    public class HeartbeatService
    {
        private readonly HttpClient _httpClient;
        private PeriodicTimer _timer;

        public HeartbeatService()
        {
            _httpClient = new HttpClient();
            // Đã đổi sang URL IP LAN của máy tính để gọi từ điện thoại thật
            _httpClient.BaseAddress = new Uri("http://192.168.1.26:5000");
        }

        public async Task StartHeartbeatAsync()
        {
            // Set nhịp tim 10 giây/lần
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            string deviceId = DeviceHelper.GetDeviceId();

            while (await _timer.WaitForNextTickAsync())
            {
                try
                {
                    // 1. Cố gắng lấy vị trí cũ trước cho nhanh
                    var location = await Geolocation.GetLastKnownLocationAsync();

                    // 2. Nếu điện thoại không có cache vị trí, bắt buộc xin định vị mới nhất (đợi tối đa 5 giây)
                    if (location == null)
                    {
                        location = await Geolocation.GetLocationAsync(new GeolocationRequest
                        {
                            DesiredAccuracy = GeolocationAccuracy.Medium,
                            Timeout = TimeSpan.FromSeconds(5)
                        });
                    }

                    // 3. Fallback: Nếu vẫn null (do trong nhà mất sóng), lấy tọa độ mặc định (0, 0) để không bị crash
                    double currentLat = location?.Latitude ?? 0;
                    double currentLng = location?.Longitude ?? 0;

                    var payload = new
                    {
                        DeviceId = deviceId,
                        Latitude = currentLat,
                        Longitude = currentLng
                    };

                    string json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Bắn dữ liệu lên Backend
                    await _httpClient.PostAsync("/api/heartbeat/ping", content);
                }
                catch (Exception ex)
                {
                    // Ghi log lỗi nếu rớt mạng, không làm crash app
                    Console.WriteLine($"Lỗi gửi Heartbeat: {ex.Message}");
                }
            }
        }
    }
}