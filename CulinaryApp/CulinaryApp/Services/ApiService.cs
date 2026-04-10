using System.Text.Json;
using CulinaryApp.Models;

namespace CulinaryApp.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;

       
        private const string BaseUrl = "http://localhost:5000/api/Poi";

        public ApiService()
        {
            _httpClient = new HttpClient();
        }

        // ĐIỂM MỚI: Thêm tham số lang, mặc định là "vi"
        public async Task<List<PoiModel>> GetAllPoisAsync(string lang = "vi")
        {
            try
            {
                // Nối tham số lang vào đuôi URL
                string url = $"{BaseUrl}?lang={lang}";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<List<PoiModel>>(content, options);
                }
                return new List<PoiModel>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LỖI GỌI API: {ex.Message}");
                return new List<PoiModel>();
            }
        }
    }
}