using System.Security.Claims;
using KampusImece.Data;
using KampusImece.Models.Entities;
using KampusImece.Models.ViewModels;
using KampusImece.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KampusImece.Controllers;

[Authorize(Roles = "Coordinator")]
[Route("[controller]")]
public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext context,
        IFileStorageService fileStorageService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(idStr, out var id) ? id : null;
    }

    /// <summary>
    /// Koordinatör / Admin Yönetim Paneli Ana Görünümü (Dashboard, Şikayetler, Kullanıcılar, İlanlar)
    /// </summary>
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] string tab = "reports")
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalEvents = await _context.CleanupEvents.CountAsync();
        var activeEvents = await _context.CleanupEvents.CountAsync(e => e.Status == EventStatus.Open || e.Status == EventStatus.QuotaFull);
        var completedEvents = await _context.CleanupEvents.CountAsync(e => e.Status == EventStatus.Completed);
        var pendingReports = await _context.EventReports.CountAsync(r => r.Status == ReportStatus.Pending);
        var resolvedReports = await _context.EventReports.CountAsync(r => r.Status == ReportStatus.Resolved);

        // Şikayetler Listesi
        var reports = await _context.EventReports
            .Include(r => r.Event)
                .ThenInclude(e => e.Creator)
            .Include(r => r.ReporterUser)
            .OrderBy(r => r.Status == ReportStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new AdminReportItemDto
            {
                ReportId = r.Id,
                EventId = r.EventId,
                EventTitle = r.Event != null ? r.Event.Title : "Silinmiş İlan",
                EventImageUrl = r.Event != null ? r.Event.ImageUrl : "",
                EventLocation = r.Event != null ? r.Event.LocationName : "",
                EventCreatorName = r.Event != null && r.Event.Creator != null ? r.Event.Creator.FullName : "-",
                EventCreatorEmail = r.Event != null && r.Event.Creator != null ? r.Event.Creator.Email : "-",
                ReporterUserId = r.ReporterUserId,
                ReporterName = r.ReporterUser != null ? r.ReporterUser.FullName : "Bilinmeyen Kullanıcı",
                ReporterEmail = r.ReporterUser != null ? r.ReporterUser.Email : "-",
                ReporterStudentNumber = r.ReporterUser != null ? r.ReporterUser.StudentNumber : "-",
                Reason = r.Reason,
                Description = r.Description,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                ReviewedAt = r.ReviewedAt,
                AdminNotes = r.AdminNotes
            })
            .ToListAsync();

        // Kullanıcılar Listesi
        var users = await _context.Users
            .Include(u => u.CreatedEvents)
            .Include(u => u.Participations)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AdminUserItemDto
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                StudentNumber = u.StudentNumber,
                PhoneNumber = u.PhoneNumber,
                IsCoordinator = u.IsCoordinator,
                ReliabilityScore = u.ReliabilityScore,
                CreatedEventsCount = u.CreatedEvents.Count,
                JoinedEventsCount = u.Participations.Count,
                IsKvkkApproved = u.IsKvkkApproved,
                KvkkApprovedAt = u.KvkkApprovedAt,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        // Tüm İlanlar Listesi
        var events = await _context.CleanupEvents
            .Include(e => e.Creator)
            .Include(e => e.Participations)
            .Include(e => e.Reports)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new AdminEventItemDto
            {
                EventId = e.Id,
                Title = e.Title,
                LocationName = e.LocationName,
                ImageUrl = e.ImageUrl,
                AfterImageUrl = e.AfterImageUrl,
                CreatorName = e.Creator != null ? e.Creator.FullName : "-",
                CreatorEmail = e.Creator != null ? e.Creator.Email : "-",
                Status = e.Status,
                ParticipantsCount = e.Participations.Count,
                RequiredVolunteers = e.RequiredVolunteers,
                ScheduledAt = e.ScheduledAt,
                CreatedAt = e.CreatedAt,
                ReportsCount = e.Reports.Count
            })
            .ToListAsync();

        var viewModel = new AdminDashboardViewModel
        {
            TotalUsersCount = totalUsers,
            TotalEventsCount = totalEvents,
            ActiveEventsCount = activeEvents,
            CompletedEventsCount = completedEvents,
            PendingReportsCount = pendingReports,
            ResolvedReportsCount = resolvedReports,
            Reports = reports,
            Users = users,
            Events = events,
            ActiveTab = string.IsNullOrWhiteSpace(tab) ? "reports" : tab.ToLower()
        };

        return View(viewModel);
    }

    /// <summary>
    /// Şikayeti çöz ve ilgili kirlilik ilanını (postunu) kalıcı olarak kaldır
    /// </summary>
    [HttpPost("Reports/{id}/Resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveReportAndRemovePost(Guid id)
    {
        var currentUserId = GetCurrentUserId();

        var report = await _context.EventReports
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null)
        {
            TempData["ErrorMessage"] = "Şikayet kaydı bulunamadı.";
            return RedirectToAction(nameof(Index), new { tab = "reports" });
        }

        // İlan varsa diskten fotoğraflarını ve veritabanından kaydını kaldır
        if (report.Event != null)
        {
            var eventToDelete = report.Event;
            var eventTitle = eventToDelete.Title;

            // Fotoğrafları sil
            await _fileStorageService.DeleteImageAsync(eventToDelete.ImageUrl);
            if (!string.IsNullOrWhiteSpace(eventToDelete.AfterImageUrl))
            {
                await _fileStorageService.DeleteImageAsync(eventToDelete.AfterImageUrl);
            }

            // Aynı etkinliğe ait diğer tüm şikayetleri de "Çözüldü" olarak güncelle
            var siblingReports = await _context.EventReports
                .Where(r => r.EventId == eventToDelete.Id)
                .ToListAsync();

            foreach (var r in siblingReports)
            {
                r.Status = ReportStatus.Resolved;
                r.ReviewedAt = DateTime.UtcNow;
                r.ReviewedByUserId = currentUserId;
                r.AdminNotes = "İlan moderatör tarafından incelendi ve platformdan kaldırıldı.";
            }

            _context.CleanupEvents.Remove(eventToDelete);
            await _context.SaveChangesAsync();

            _logger.LogWarning("Admin tarafından şikayet üzerine ilan kaldırıldı: EventId={EventId}, Title={Title}, ModeratorId={ModId}",
                eventToDelete.Id, eventTitle, currentUserId);

            TempData["SuccessMessage"] = $"\"{eventTitle}\" başlıklı ilan kaldırıldı ve şikayet 'Çözüldü' olarak arşivlendi.";
        }
        else
        {
            report.Status = ReportStatus.Resolved;
            report.ReviewedAt = DateTime.UtcNow;
            report.ReviewedByUserId = currentUserId;
            report.AdminNotes = "İlan zaten sistemde bulunmuyor; şikayet kapatıldı.";
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Şikayet arşivlendi.";
        }

        return RedirectToAction(nameof(Index), new { tab = "reports" });
    }

    /// <summary>
    /// Şikayeti incele ve reddet (İlan kalır, kural ihlali olmadığı teyit edilir)
    /// </summary>
    [HttpPost("Reports/{id}/Dismiss")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissReport(Guid id)
    {
        var currentUserId = GetCurrentUserId();

        var report = await _context.EventReports.FindAsync(id);
        if (report == null)
        {
            TempData["ErrorMessage"] = "Şikayet kaydı bulunamadı.";
            return RedirectToAction(nameof(Index), new { tab = "reports" });
        }

        report.Status = ReportStatus.Dismissed;
        report.ReviewedAt = DateTime.UtcNow;
        report.ReviewedByUserId = currentUserId;
        report.AdminNotes = "Şikayet incelendi; herhangi bir topluluk kuralı ihlali görülmedi.";

        await _context.SaveChangesAsync();

        _logger.LogInformation("Şikayet reddedildi: ReportId={ReportId}, ModeratorId={ModId}", id, currentUserId);

        TempData["SuccessMessage"] = "Şikayet reddedildi ve kapatıldı. İlan yayında kalmaya devam ediyor.";
        return RedirectToAction(nameof(Index), new { tab = "reports" });
    }

    /// <summary>
    /// Admin yetkisiyle doğrudan bir ilanı kaldırma (POST /Admin/Events/{id}/Delete)
    /// </summary>
    [HttpPost("Events/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        var ev = await _context.CleanupEvents.FindAsync(id);
        if (ev == null)
        {
            TempData["ErrorMessage"] = "İlan bulunamadı veya daha önce silinmiş.";
            return RedirectToAction(nameof(Index), new { tab = "events" });
        }

        var title = ev.Title;

        // Görselleri temizle
        await _fileStorageService.DeleteImageAsync(ev.ImageUrl);
        if (!string.IsNullOrWhiteSpace(ev.AfterImageUrl))
        {
            await _fileStorageService.DeleteImageAsync(ev.AfterImageUrl);
        }

        _context.CleanupEvents.Remove(ev);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Admin tarafından ilan kaldırıldı: EventId={EventId}, Title={Title}", id, title);

        TempData["SuccessMessage"] = $"\"{title}\" başlıklı ilan başarıyla platformdan kaldırıldı.";
        return RedirectToAction(nameof(Index), new { tab = "events" });
    }

    /// <summary>
    /// Admin yetkisiyle kullanıcıyı sistemden çıkarma (POST /Admin/Users/{id}/Delete)
    /// Kullanıcının oluşturduğu etkinlikler, görselleri ve katılımları temizlenir.
    /// </summary>
    [HttpPost("Users/{id}/Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId.HasValue && currentUserId.Value == id)
        {
            TempData["ErrorMessage"] = "Kendi hesabınızı yönetim panelinden silemezsiniz!";
            return RedirectToAction(nameof(Index), new { tab = "users" });
        }

        var user = await _context.Users
            .Include(u => u.CreatedEvents)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Silinmek istenen kullanıcı bulunamadı.";
            return RedirectToAction(nameof(Index), new { tab = "users" });
        }

        if (user.Email.Equals("ysert20051@gmail.com", StringComparison.OrdinalIgnoreCase) ||
            user.Email.Equals("233311021@ogr.selcuk.edu.tr", StringComparison.OrdinalIgnoreCase) ||
            user.Email.Equals("233311070@ogr.selcuk.edu.tr", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Ana koordinatör / yönetici hesapları sistemden silinemez!";
            return RedirectToAction(nameof(Index), new { tab = "users" });
        }

        var userName = user.FullName;

        // Kullanıcının oluşturduğu tüm etkinliklerin fotoğraflarını diskten sil
        foreach (var ev in user.CreatedEvents)
        {
            await _fileStorageService.DeleteImageAsync(ev.ImageUrl);
            if (!string.IsNullOrWhiteSpace(ev.AfterImageUrl))
            {
                await _fileStorageService.DeleteImageAsync(ev.AfterImageUrl);
            }
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Admin tarafından kullanıcı sistemden çıkarıldı: UserId={UserId}, FullName={FullName}, ModeratorId={ModId}",
            id, userName, currentUserId);

        TempData["SuccessMessage"] = $"\"{userName}\" isimli kullanıcı ve ilişkili tüm verileri sistemden başarıyla çıkarıldı.";
        return RedirectToAction(nameof(Index), new { tab = "users" });
    }
}
