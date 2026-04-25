using MongoDB.Driver;
using CulinaryBackend.Models;

namespace CulinaryBackend.Services
{
    public class QrScanLogService
    {
        private readonly IMongoCollection<QrScanLog> _qrLogs;

        public QrScanLogService(IMongoDatabase database)
        {
            // Tạo một collection (bảng) mới tên là "QrScanLogs" trong MongoDB
            _qrLogs = database.GetCollection<QrScanLog>("QrScanLogs");
        }

        // Hàm ghi nhận lượt quét mã QR
        public async Task LogScanAsync(string poiId, string deviceId)
        {
            var log = new QrScanLog
            {
                PoiId = poiId,
                DeviceId = deviceId,
                ScannedAt = DateTime.UtcNow
            };
            await _qrLogs.InsertOneAsync(log);
        }
    }
}