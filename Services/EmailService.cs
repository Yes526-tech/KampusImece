using System.Net;
using System.Net.Mail;
using System.Text;
using KampusImece.Models.Entities;

namespace KampusImece.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IWebHostEnvironment _env;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _logger = logger;
        _env = env;
    }

    public async Task SendReportNotificationAsync(string toEmail, EventReport report, CleanupEvent cleanupEvent, AppUser reporter)
    {
        var targetEmail = !string.IsNullOrWhiteSpace(toEmail)
            ? toEmail
            : _configuration["EmailSettings:AdminNotificationEmail"] ?? "ysert20051@gmail.com";

        var subject = $"[Kampüsİmece Acil Şikayet] \"{cleanupEvent.Title}\" Bildirildi";

        var htmlBuilder = new StringBuilder();
        htmlBuilder.AppendLine("<!DOCTYPE html>");
        htmlBuilder.AppendLine("<html lang='tr'>");
        htmlBuilder.AppendLine("<head><meta charset='utf-8'><title>Kirlilik İlanı Şikayet Bildirimi</title></head>");
        htmlBuilder.AppendLine("<body style='font-family: -apple-system, BlinkMacSystemFont, Segoe UI, Roboto, sans-serif; background-color: #f8fafc; margin: 0; padding: 24px; color: #1e293b;'>");
        htmlBuilder.AppendLine("  <div style='max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 16px; border: 1px solid #e2e8f0; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05);'>");
        
        // Üst Başlık
        htmlBuilder.AppendLine("    <div style='background: #dc2626; padding: 20px 24px; color: #ffffff;'>");
        htmlBuilder.AppendLine("      <h2 style='margin: 0; font-size: 18px; font-weight: 700;'>Kampüsİmece Moderasyon Uyarısı</h2>");
        htmlBuilder.AppendLine("      <p style='margin: 4px 0 0; font-size: 13px; opacity: 0.9;'>Kullanıcı tarafından uygunsuz içerik / kirlilik ilanı şikayeti yapıldı.</p>");
        htmlBuilder.AppendLine("    </div>");

        // Gövde
        htmlBuilder.AppendLine("    <div style='padding: 24px;'>");
        htmlBuilder.AppendLine("      <div style='background: #fef2f2; border: 1px solid #fee2e2; border-radius: 12px; padding: 14px; margin-bottom: 20px;'>");
        htmlBuilder.AppendLine($"        <strong style='color: #991b1b; font-size: 13px;'>Şikayet Nedeni:</strong>");
        htmlBuilder.AppendLine($"        <p style='margin: 4px 0 0; font-size: 14px; font-weight: 600; color: #b91c1c;'>{report.Reason}</p>");
        if (!string.IsNullOrWhiteSpace(report.Description))
        {
            htmlBuilder.AppendLine($"        <p style='margin: 8px 0 0; font-size: 13px; color: #4b5563; font-style: italic;'>\"{report.Description}\"</p>");
        }
        htmlBuilder.AppendLine("      </div>");

        // İlan Bilgileri Tablosu
        htmlBuilder.AppendLine("      <h3 style='font-size: 14px; font-weight: 700; margin: 0 0 12px; color: #0f172a;'>Şikayet Edilen İlan</h3>");
        htmlBuilder.AppendLine("      <table style='width: 100%; border-collapse: collapse; font-size: 13px; margin-bottom: 20px;'>");
        htmlBuilder.AppendLine($"        <tr style='border-bottom: 1px solid #f1f5f9;'><td style='padding: 8px 0; color: #64748b;'>Başlık:</td><td style='padding: 8px 0; font-weight: 600;'>{cleanupEvent.Title}</td></tr>");
        htmlBuilder.AppendLine($"        <tr style='border-bottom: 1px solid #f1f5f9;'><td style='padding: 8px 0; color: #64748b;'>Konum:</td><td style='padding: 8px 0;'>{cleanupEvent.LocationName}</td></tr>");
        htmlBuilder.AppendLine($"        <tr style='border-bottom: 1px solid #f1f5f9;'><td style='padding: 8px 0; color: #64748b;'>Açıklama:</td><td style='padding: 8px 0;'>{cleanupEvent.Description}</td></tr>");
        htmlBuilder.AppendLine($"        <tr style='border-bottom: 1px solid #f1f5f9;'><td style='padding: 8px 0; color: #64748b;'>İlan Sahibi:</td><td style='padding: 8px 0;'>{cleanupEvent.Creator?.FullName} ({cleanupEvent.Creator?.Email})</td></tr>");
        htmlBuilder.AppendLine($"        <tr style='border-bottom: 1px solid #f1f5f9;'><td style='padding: 8px 0; color: #64748b;'>Şikayet Eden:</td><td style='padding: 8px 0;'>{reporter.FullName} ({reporter.Email} - {reporter.StudentNumber})</td></tr>");
        htmlBuilder.AppendLine($"        <tr><td style='padding: 8px 0; color: #64748b;'>Tarih:</td><td style='padding: 8px 0;'>{report.CreatedAt:dd.MM.yyyy HH:mm} (UTC)</td></tr>");
        htmlBuilder.AppendLine("      </table>");

        // Eylem Butonu
        htmlBuilder.AppendLine("      <div style='text-align: center; margin: 28px 0 12px;'>");
        htmlBuilder.AppendLine("        <a href='http://localhost:5247/Admin' style='background: #059669; color: #ffffff; text-decoration: none; padding: 12px 24px; border-radius: 10px; font-weight: 600; font-size: 13px; display: inline-block;'>Yönetim Panelinden İncele & Kaldır</a>");
        htmlBuilder.AppendLine("      </div>");
        htmlBuilder.AppendLine("    </div>");

        // Footer
        htmlBuilder.AppendLine("    <div style='background: #f8fafc; border-top: 1px solid #f1f5f9; padding: 16px 24px; font-size: 11px; color: #94a3b8; text-align: center;'>");
        htmlBuilder.AppendLine("      Bu e-posta Kampüsİmece Topluma Hizmet Platformu Güvenlik Servisi tarafından otomatik üretilmiştir.");
        htmlBuilder.AppendLine("    </div>");
        htmlBuilder.AppendLine("  </div>");
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");

        await SendEmailAsync(targetEmail, subject, htmlBuilder.ToString(), isHtml: true);
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true)
    {
        var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
        var smtpPort = int.TryParse(_configuration["EmailSettings:SmtpPort"], out var port) ? port : 587;
        var enableSsl = bool.TryParse(_configuration["EmailSettings:EnableSsl"], out var ssl) ? ssl : true;
        var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "kampusimece.noreply@gmail.com";
        var senderDisplayName = _configuration["EmailSettings:SenderDisplayName"] ?? "Kampüsİmece Moderasyon";
        var username = _configuration["EmailSettings:Username"];
        var password = _configuration["EmailSettings:Password"];

        _logger.LogInformation("================ EMAIL BILDIRIMI GONDERILIYOR ================");
        _logger.LogInformation("KIME: {ToEmail}", toEmail);
        _logger.LogInformation("KONU: {Subject}", subject);
        _logger.LogInformation("==============================================================");

        // Denetim amacıyla e-posta günlüğünü wwwroot/uploads/email_logs/ klasörüne de kaydet
        try
        {
            var logDir = Path.Combine(_env.WebRootPath, "uploads", "email_logs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            var fileName = $"email_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.html";
            var filePath = Path.Combine(logDir, fileName);
            await File.WriteAllTextAsync(filePath, body);
            _logger.LogInformation("E-posta içerik kopyası kaydedildi: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "E-posta yerel log dosyasına kaydedilemedi.");
        }

        // SMTP kimlik bilgileri verilmişse gerçek SMTP gönderimi yap
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            try
            {
                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = enableSsl,
                    Timeout = 8000
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderDisplayName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = isHtml
                };
                mailMessage.To.Add(toEmail);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("E-posta {ToEmail} adresine SMTP üzerinden başarıyla iletildi.", toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMTP ile e-posta gönderimi başarısız oldu. Ancak e-posta loga ve sisteme kaydedildi.");
            }
        }
        else
        {
            _logger.LogInformation("SMTP kullanıcı adı/şifre yapılandırılmadığından e-posta geliştirme modunda simüle edildi ve loglandı.");
        }
    }
}
