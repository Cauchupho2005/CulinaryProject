namespace CulinaryApp.Helpers
{
    public static class DeviceHelper
    {
        public static string GetDeviceId()
        {
            // Kiểm tra xem máy này đã có ID chưa
            string deviceId = Preferences.Get("DeviceId", string.Empty);

            // Nếu chưa có (mở app lần đầu), tạo mới và lưu lại
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                Preferences.Set("DeviceId", deviceId);
            }

            return deviceId;
        }
    }
}