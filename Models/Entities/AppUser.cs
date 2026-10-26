using System.ComponentModel.DataAnnotations;

namespace KampusImece.Models.Entities;

public class AppUser
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string StudentNumber { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public int ReliabilityScore { get; set; } = 100;

    /// <summary>
    /// Proje teklif formundaki proje koordinatörü ve moderasyon yetkisi
    /// </summary>
    public bool IsCoordinator { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 6698 Sayılı KVKK Kapsamında Kullanıcının Aydınlatma Metnini ve Açık Rıza Beyanını Onay Durumu
    /// </summary>
    public bool IsKvkkApproved { get; set; } = true;

    /// <summary>
    /// KVKK onayının verildiği kesin UTC zaman damgası (Denetim İzi / Audit Trail)
    /// </summary>
    public DateTime KvkkApprovedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Kullanıcının onayladığı KVKK metni versiyonu
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string KvkkVersion { get; set; } = "v1.0";

    /// <summary>
    /// KVKK onayının SHA-256 kriptografik denetim ve bütünlük özeti (Audit Hash)
    /// </summary>
    [MaxLength(128)]
    public string? KvkkConsentHash { get; set; }

    // Navigation Properties
    public ICollection<CleanupEvent> CreatedEvents { get; set; } = new List<CleanupEvent>();
    public ICollection<EventParticipation> Participations { get; set; } = new List<EventParticipation>();
    public ICollection<EventReport> SubmittedReports { get; set; } = new List<EventReport>();
}
