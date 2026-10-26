using KampusImece.Models.Entities;

namespace KampusImece.Services;

public interface IEmailService
{
    Task SendReportNotificationAsync(string toEmail, EventReport report, CleanupEvent cleanupEvent, AppUser reporter);
    Task SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true);
}
