using System.Text.Json;
using CulinaryApp.Models;
using Microsoft.Maui.Networking; 
using Microsoft.Maui.Storage; 

namespace CulinaryApp.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "http://10.166.210.134:5000/api/Poi";

        public ApiService()
        {
            _httpClient = new HttpClient();
           
        }

        public async Task<List<PoiModel>> GetAllPoisAsync(string lang = "vi")
        {
            string url = $"{BaseUrl}?lang={lang}";
            string cacheKey = $"pois_{lang}"; 

            // 1. KIỂM TRA MẠNG: Lấy data từ máy nếu mất mạng
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                return GetOfflineData(cacheKey);
            }

            // 2. CÓ MẠNG: Gọi API
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var pois = JsonSerializer.Deserialize<List<PoiModel>>(content, options);

                    // LƯU OFFLINE BẰNG MAUI PREFERENCES (An toàn & Xịn hơn MonkeyCache)
                    if (pois != null)
                    {
                        // Biến danh sách thành chuỗi JSON và cất vào máy
                        string jsonToSave = JsonSerializer.Serialize(pois);
                        Preferences.Default.Set(cacheKey, jsonToSave);
                    }

                    return pois;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LỖI API: {ex.Message}");
            }

            // Nếu API lỗi, vẫn ráng moi data cũ ra dùng tạm
            return GetOfflineData(cacheKey);
        }

        // --- HÀM PHỤ ĐỂ ĐỌC DATA OFFLINE ---
        private List<PoiModel> GetOfflineData(string key)
        {
            // Lấy chuỗi JSON từ bộ nhớ máy ra
            string savedJson = Preferences.Default.Get(key, string.Empty);

            // Nếu có dữ liệu thì dịch ngược lại thành danh sách PoiModel
            if (!string.IsNullOrEmpty(savedJson))
            {
                return JsonSerializer.Deserialize<List<PoiModel>>(savedJson) ?? new List<PoiModel>();
            }

            return new List<PoiModel>();
        }
    }
}