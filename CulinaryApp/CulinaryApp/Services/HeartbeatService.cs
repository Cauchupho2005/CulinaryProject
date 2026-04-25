using System.Text;
using System.Text.Json;
using CulinaryApp.Helpers;
using Microsoft.Maui.Devices.Sensors;

namespace CulinaryApp.Services
{
    public class HeartbeatService
    {
        private readonly HttpClient _httpClient;
        private PeriodicTimer? _timer;
        private bool _isRunning = false; // CHỐT CHẶN: Đảm bảo chỉ có 1 luồng chạy duy nhất

        public HeartbeatService()
        {
            _httpClient = new HttpClient();
            // Sử dụng IP chuẩn 4G hiện tại của Sếp
            _httpClient.BaseAddress = new Uri("http://10.166.210.134:5000");
        }

        public async Task StartHeartbeatAsync()
        {
            // Nếu Service đang chạy rồi thì thoát ra, không tạo thêm Timer mới
            if (_isRunning) return;
            _isRunning = true;

            _timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            string deviceId = DeviceHelper.GetDeviceId();

            while (await _timer.WaitForNextTickAsync())
            {
                try
                {
                    // Lấy vị trí GPS
                    var location = await Geolocation.GetLastKnownLocationAsync();
                    if (location == null)
                    {
                        location = await Geolocation.GetLocationAsync(new GeolocationRequest
                        {
                            DesiredAccuracy = GeolocationAccuracy.Medium,
                            Timeout = TimeSpan.FromSeconds(5)
                        });
                    }

                    double currentLat = location?.Latitude ?? 0;
                    double currentLng = location?.Longitude ?? 0;

                    // Chuẩn bị gói tin gửi lên Backend
                    var payload = new { DeviceId = deviceId, Latitude = currentLat, Longitude = currentLng };
                    string json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Bắn dữ liệu về máy chủ
                    await _httpClient.PostAsync("/api/heartbeat/ping", content);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi gửi Heartbeat: {ex.Message}");
                }
            }
        }
    }
}