using System.Collections.Concurrent;

namespace CulinaryBackend.Services
{
    // Thêm class này để gói dữ liệu tọa độ và thời gian
    public class UserLocationInfo
    {
        public DateTime LastSeen { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class ActiveUserTracker
    {
        // Đổi sang lưu UserLocationInfo thay vì chỉ DateTime
        private readonly ConcurrentDictionary<string, UserLocationInfo> _activeUsers = new();

        public ActiveUserTracker()
        {
        }

        public void Ping(string deviceId, double lat, double lng)
        {
            _activeUsers[deviceId] = new UserLocationInfo
            {
                LastSeen = DateTime.UtcNow,
                Latitude = lat,
                Longitude = lng
            };
        }

        public int GetActiveUserCount()
        {
            CleanUpDeadUsers();
            return _activeUsers.Count;
        }

        // Lấy danh sách điểm nhiệt để vẽ bản đồ
        public List<double[]> GetHeatmapData()
        {
            CleanUpDeadUsers();
            
            return _activeUsers.Values
                .Where(u => u.Latitude != 0 && u.Longitude != 0) 
                .Select(u => new double[] { u.Latitude, u.Longitude, 1.0 })
                .ToList();
        }

        // Hàm dọn dẹp những thiết bị mất tín hiệu quá 30 giây
        private void CleanUpDeadUsers()
        {
            var threshold = DateTime.UtcNow.AddSeconds(-30);
            var deadUsers = _activeUsers.Where(kvp => kvp.Value.LastSeen < threshold).Select(kvp => kvp.Key).ToList();

            foreach (var dead in deadUsers)
            {
                _activeUsers.TryRemove(dead, out _);
            }
        }
    }
}