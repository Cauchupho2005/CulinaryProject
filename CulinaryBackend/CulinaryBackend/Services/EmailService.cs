using System.Net;
using System.Net.Mail;

namespace CulinaryBackend.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendApprovalEmailAsync(string ownerEmail, string poiTitle, bool isApproved)
        {
            var subject = isApproved
                ? $"✅ Quán '{poiTitle}' đã được duyệt!"
                : $"❌ Quán '{poiTitle}' bị từ chối";

            var body = isApproved
                ? $@"<h2>Chúc mừng!</h2>
                     <p>Quán <strong>{poiTitle}</strong> của bạn đã được duyệt và hiển thị trên ứng dụng Culinary.</p>
                     <p>Khách hàng có thể tìm thấy quán của bạn ngay bây giờ!</p>"
                : $@"<h2>Thông báo</h2>
                     <p>Rất tiếc, quán <strong>{poiTitle}</strong> của bạn chưa được duyệt.</p>
                     <p>Vui lòng kiểm tra lại thông tin hoặc liên hệ admin để biết chi tiết.</p>";

            await SendEmailAsync(ownerEmail, subject, body);
        }

        public async Task SendPoiEventEmailAsync(string poiTitle, string actionName, string? status = null)
        {
            var notifyEmail = _config["Email:NotificationEmail"] ?? _config["Email:FromEmail"] ?? "noreply@culinary.com";
            var statusText = string.IsNullOrWhiteSpace(status) ? "không rõ" : status;
            var subject = $"[Culinary] POI vừa được {actionName}: {poiTitle}";
            var body = $@"<h3>Cập nhật POI</h3>
                         <p><strong>Hành động:</strong> {actionName}</p>
                         <p><strong>Quán:</strong> {poiTitle}</p>
                         <p><strong>Trạng thái hiện tại:</strong> {statusText}</p>
                         <p><strong>Thời gian:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>";

            await SendEmailAsync(notifyEmail, subject, body);
        }

        public async Task SendCustomEmailAsync(string toEmail, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
                return;

            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendUserAccountEventEmailAsync(string userEmail, string username, string action)
        {
            if (string.IsNullOrWhiteSpace(userEmail))
                return;

            var subject = $"[Culinary] Tài khoản của bạn vừa được {action}";
            var body = $@"<h3>Thông báo tài khoản</h3>
                         <p>Xin chào <strong>{username}</strong>,</p>
                         <p>Tài khoản của bạn vừa được <strong>{action}</strong>.</p>
                         <p>Thời gian: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>";

            await SendEmailAsync(userEmail, subject, body);
        }

        private async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                var smtpHost = _config["Email:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "587");
                var fromEmail = _config["Email:FromEmail"] ?? "noreply@culinary.com";
                var fromPassword = _config["Email:Password"] ?? "";

                using var smtp = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(fromEmail, fromPassword),
                    EnableSsl = true
                };

                var message = new MailMessage(fromEmail, toEmail, subject, body)
                {
                    IsBodyHtml = true
                };

                await smtp.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email error: {ex.Message}");
            }
        }
    }
}
