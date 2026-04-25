using Microsoft.Maui.Storage;
using System;

namespace CulinaryApp.Helpers
{
    public static class DeviceHelper
    {
        public static string GetDeviceId()
        {
            // 1. Kiểm tra xem máy đã có ID lưu trong bộ nhớ chưa
            string deviceId = Preferences.Default.Get("Device_ID", string.Empty);

            // 2. Nếu chưa có (lần đầu cài app), tạo mới và lưu lại vĩnh viễn
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                Preferences.Default.Set("Device_ID", deviceId);
            }

            return deviceId;
        }
    }
}