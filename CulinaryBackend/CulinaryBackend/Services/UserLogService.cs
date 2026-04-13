using MongoDB.Driver;
using CulinaryBackend.Models;

namespace CulinaryBackend.Services
{
    public class UserLogService
    {
        private readonly IMongoCollection<UserLog> _logs;

        public UserLogService(IMongoDatabase database)
        {
            _logs = database.GetCollection<UserLog>("UserLogs");
        }

        public async Task LogActionAsync(string username, string action, string details = "", string device = "Web Admin")
        {
            var log = new UserLog
            {
                Username = username,
                Action = action,
                Details = details,
                DeviceInfo = device,
                Timestamp = DateTime.UtcNow
            };
            await _logs.InsertOneAsync(log);
        }

        public async Task<List<UserLog>> GetAllLogsAsync()
        {
            return await _logs.Find(_ => true).SortByDescending(l => l.Timestamp).ToListAsync();
        }
    }
}