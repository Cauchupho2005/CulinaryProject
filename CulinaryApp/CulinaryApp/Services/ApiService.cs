using System.Text.Json;
using CulinaryApp.Models;
using MonkeyCache.FileStore; // Thêm cái này
using Microsoft.Maui.Networking; // Để kiểm tra mạng

namespace CulinaryApp.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://culinary-api-backend.onrender.com/api/Poi";

        public ApiService()
        {
            _httpClient = new HttpClient();
            // Khởi tạo nơi lưu trữ offline (đặt tên app của bạn)
            Barrel.ApplicationId = "culinary_app_cache";
        }

        public async Task<List<PoiModel>> GetAllPoisAsync(string lang = "vi")
        {
            string url = $"{BaseUrl}?lang={lang}";
            string cacheKey = $"pois_{lang}"; // Mỗi ngôn ngữ một bản cache riêng

            // 1. KIỂM TRA MẠNG: Nếu KHÔNG có mạng, lấy ngay data trong máy
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                return Barrel.Current.Get<List<PoiModel>>(cacheKey) ?? new List<PoiModel>();
            }

            // 2. CÓ MẠNG: Tiến hành gọi API như bình thường
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var pois = JsonSerializer.Deserialize<List<PoiModel>>(content, options);

                    // LƯU DATA VÀO MÁY: Để dành cho lúc mất mạng (hết hạn sau 7 ngày)
                    if (pois != null)
                        Barrel.Current.Add(cacheKey, pois, TimeSpan.FromDays(7));

                    return pois;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LỖI API: {ex.Message}");
            }

            // Nếu gọi API lỗi nhưng trong máy có data cũ thì vẫn lấy ra dùng tạm
            return Barrel.Current.Get<List<PoiModel>>(cacheKey) ?? new List<PoiModel>();
        }
    }
}