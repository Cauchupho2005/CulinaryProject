using System.Text.Json;
using CulinaryApp.Models;

namespace CulinaryApp.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        //http ://10.44.208.134:5000/api/Poi
        //http ://192.168.1.33:5000/api/Poi
        private const string BaseUrl = "http://10.44.208.134:5000/api/Poi";

        public ApiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<List<PoiModel>> GetAllPoisAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(BaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    // Thêm tùy chọn này để lỡ Backend viết hoa/viết thường (title vs Title) MAUI vẫn đọc được hết
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