using System.Text.Json;
using CulinaryApp.Models;
using Microsoft.Maui.Networking;
using Microsoft.Maui.Storage;

namespace CulinaryApp.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        // Bạn nhớ kiểm tra lại IP này cho đúng với Laptop của bạn nhé
        private const string BaseUrl = "http://10.192.152.134:5000/api/Poi";

        public ApiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<List<PoiModel>> GetAllPoisAsync(string lang = "vi")
        {
            string url = $"{BaseUrl}?lang={lang}";
            string cacheKey = $"pois_{lang}";

            // 1. KIỂM TRA MẠNG: Mất mạng thì lấy từ Preferences (Cache cũ)
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

                    // LƯU CACHE VÀO MÁY
                    if (pois != null)
                    {
                        string jsonToSave = JsonSerializer.Serialize(pois);
                        Preferences.Default.Set(cacheKey, jsonToSave);
                    }

                    return pois ?? new List<PoiModel>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LỖI API: {ex.Message}");
            }

            return GetOfflineData(cacheKey);
        }

        private List<PoiModel> GetOfflineData(string key)
        {
            string savedJson = Preferences.Default.Get(key, string.Empty);
            if (!string.IsNullOrEmpty(savedJson))
            {
                return JsonSerializer.Deserialize<List<PoiModel>>(savedJson) ?? new List<PoiModel>();
            }
            return new List<PoiModel>();
        }
    }
}