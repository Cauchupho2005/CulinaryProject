using System.Collections.Concurrent;

namespace CulinaryBackend.Services
{
    public class ActiveUserTracker
    {
        // Dùng ConcurrentDictionary lưu thời điểm cuối cùng thiết bị gửi nhịp tim
        private readonly ConcurrentDictionary<string, DateTime> _activeKeys = new();

        // Không cần IMemoryCache nữa
        public ActiveUserTracker()
        {
        }

        // Hàm này bắt buộc phải giữ nguyên tham số (deviceId, lat, lng) để không bị lỗi với Controller
        public void Ping(string deviceId, double lat, double lng)
        {
            // Cập nhật mốc thời gian mới nhất (UTC) cho thiết bị này
            _activeKeys[deviceId] = DateTime.UtcNow;
        }

        public int GetActiveUserCount()
        {
            // Ngưỡng 20 giây: Những ai gửi tín hiệu trước mốc này sẽ bị coi là đã offline
            var threshold = DateTime.UtcNow.AddSeconds(-20);

            // 1. Tìm các thiết bị đã quá hạn 20 giây (Ghost users)
            var deadUsers = _activeKeys.Where(kvp => kvp.Value < threshold).Select(kvp => kvp.Key).ToList();

            // 2. Chủ động xóa sổ các thiết bị này khỏi bộ nhớ để Web Admin hạ số người xuống
            foreach (var dead in deadUsers)
            {
                _activeKeys.TryRemove(dead, out _);
            }

            // Trả về đúng số lượng người dùng đang thực sự online
            return _activeKeys.Count;
        }
    }
}