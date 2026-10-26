using System.Security.Claims;
using KampusImece.Data;
using KampusImece.Models.Entities;
using KampusImece.Models.ViewModels;
using KampusImece.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KampusImece.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<ReportsController> _logger;
    private readonly IConfiguration _configuration;

    public ReportsController(
        AppDbContext context, 
        IEmailService emailService, 
        ILogger<ReportsController> logger,
        IConfiguration configuration)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Kullanıcının kirlilik ilanını şikayet etmesi (POST /api/reports)
    /// </summary>
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReport([FromForm] CreateReportDto model)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(new { success = false, message = "Form doğrulama hatası.", errors });
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdString, out var currentUserId))
        {
            return Unauthorized(new { success = false, message = "Oturum açmanız gerekmektedir." });
        }

        var reporter = await _context.Users.FindAsync(currentUserId);
        if (reporter == null)
        {
            return Unauthorized(new { success = false, message = "Kullanıcı bulunamadı." });
        }

        var cleanupEvent = await _context.CleanupEvents
            .Include(e => e.Creator)
            .FirstOrDefaultAsync(e => e.Id == model.EventId);

        if (cleanupEvent == null)
        {
            return NotFound(new { success = false, message = "Şikayet edilmek istenen kirlilik ilanı bulunamadı veya silinmiş." });
        }

        // Aynı kullanıcının aynı ilan için mükerrer bekleyen şikayet açmasını engelle
        var existingPendingReport = await _context.EventReports
            .FirstOrDefaultAsync(r => r.EventId == model.EventId && r.ReporterUserId == currentUserId && r.Status == ReportStatus.Pending);

        if (existingPendingReport != null)
        {
            return BadRequest(new { success = false, message = "Bu ilan için zaten bekleyen bir şikayetiniz bulunmaktadır. Moderatörlerimiz incelemektedir." });
        }

        var report = new EventReport
        {
            Id = Guid.NewGuid(),
            EventId = cleanupEvent.Id,
            ReporterUserId = currentUserId,
            Reason = model.Reason.Trim(),
            Description = model.Description?.Trim(),
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.EventReports.Add(report);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Yeni şikayet kaydı oluşturuldu: ReportId={ReportId}, EventId={EventId}, Reporter={ReporterEmail}",
            report.Id, cleanupEvent.Id, reporter.Email);

        // Koordinatörün e-posta adresine acil bildirim ilet
        var adminEmail = _configuration["EmailSettings:AdminNotificationEmail"] ?? "ysert20051@gmail.com";
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendReportNotificationAsync(adminEmail, report, cleanupEvent, reporter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Şikayet e-posta bildirimi arka planda gönderilirken hata oluştu.");
            }
        });

        return Ok(new
        {
            success = true,
            message = "Şikayetiniz koordinatörlerimize ve moderasyon ekibimize iletildi. Hassasiyetiniz için teşekkür ederiz."
        });
    }
}
