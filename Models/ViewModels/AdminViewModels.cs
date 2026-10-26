using System.ComponentModel.DataAnnotations;
using KampusImece.Models.Entities;

namespace KampusImece.Models.ViewModels;

public class CreateReportDto
{
    [Required(ErrorMessage = "Şikayet edilecek ilan belirtilmelidir.")]
    public Guid EventId { get; set; }

    [Required(ErrorMessage = "Lütfen bir şikayet nedeni seçiniz.")]
    [MaxLength(100, ErrorMessage = "Şikayet nedeni en fazla 100 karakter olabilir.")]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Şikayet açıklaması en fazla 1000 karakter olabilir.")]
    public string? Description { get; set; }
}

public class AdminDashboardViewModel
{
    public int TotalUsersCount { get; set; }
    public int TotalEventsCount { get; set; }
    public int ActiveEventsCount { get; set; }
    public int CompletedEventsCount { get; set; }
    public int PendingReportsCount { get; set; }
    public int ResolvedReportsCount { get; set; }

    public List<AdminReportItemDto> Reports { get; set; } = new();
    public List<AdminUserItemDto> Users { get; set; } = new();
    public List<AdminEventItemDto> Events { get; set; } = new();

    public string ActiveTab { get; set; } = "reports";
}

public class AdminReportItemDto
{
    public Guid ReportId { get; set; }
    public Guid EventId { get; set; }
    public string EventTitle { get; set; } = string.Empty;
    public string EventImageUrl { get; set; } = string.Empty;
    public string EventLocation { get; set; } = string.Empty;
    public string EventCreatorName { get; set; } = string.Empty;
    public string EventCreatorEmail { get; set; } = string.Empty;

    public Guid ReporterUserId { get; set; }
    public string ReporterName { get; set; } = string.Empty;
    public string ReporterEmail { get; set; } = string.Empty;
    public string ReporterStudentNumber { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReportStatus Status { get; set; }
    public string StatusText => Status switch
    {
        ReportStatus.Pending => "İnceleniyor (Beklemede)",
        ReportStatus.Resolved => "Çözüldü (İlan Kaldırıldı)",
        ReportStatus.Dismissed => "Reddedildi / İhlal Yok",
        _ => "Bilinmiyor"
    };

    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? AdminNotes { get; set; }
}

public class AdminUserItemDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsCoordinator { get; set; }
    public int ReliabilityScore { get; set; }
    public int CreatedEventsCount { get; set; }
    public int JoinedEventsCount { get; set; }
    public bool IsKvkkApproved { get; set; }
    public DateTime KvkkApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminEventItemDto
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public string? AfterImageUrl { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public string CreatorEmail { get; set; } = string.Empty;
    public EventStatus Status { get; set; }
    public string StatusText => Status switch
    {
        EventStatus.Open => "Gönüllü Aranıyor",
        EventStatus.QuotaFull => "Kontenjan Doldu",
        EventStatus.Completed => "Tamamlandı",
        _ => "İptal"
    };
    public int ParticipantsCount { get; set; }
    public int RequiredVolunteers { get; set; }
    public DateTime ScheduledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ReportsCount { get; set; }
}
