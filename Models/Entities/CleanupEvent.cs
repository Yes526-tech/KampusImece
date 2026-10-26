using System.ComponentModel.DataAnnotations;

namespace KampusImece.Models.Entities;

public class CleanupEvent
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Kirlilik noktası çöp yığınının ilk (Öncesi / Before) fotoğrafı
    /// </summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Temizlik tamamlandıktan sonra yüklenen (Sonrası / After) kanıt fotoğrafı
    /// </summary>
    public string? AfterImageUrl { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    [Required]
    [MaxLength(200)]
    public string LocationName { get; set; } = string.Empty;

    [Range(1, 50)]
    public int RequiredVolunteers { get; set; }

    public DateTime ScheduledAt { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Open;

    /// <summary>
    /// Teklif formunda risk önlemi olarak belirtilen sahte/uygunsuz ilan moderasyon onayı
    /// </summary>
    public bool IsApproved { get; set; } = true;

    public Guid CreatorId { get; set; }
    public AppUser Creator { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Temizliğin tamamlandığı ve kanıtın sisteme yüklendiği tarih/saat
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Temizlik kanıt fotoğrafını bizzat yükleyen kullanıcının kimliği (+10 ekstra puan alır)
    /// </summary>
    public Guid? CompletedByUserId { get; set; }

    /// <summary>
    /// Temizlik sonuç notu (Örn: "4 poşet plastik ve cam atık toplandı, geri dönüşüme teslim edildi.")
    /// </summary>
    [MaxLength(500)]
    public string? CompletionNote { get; set; }

    // Navigation Properties
    public ICollection<EventParticipation> Participations { get; set; } = new List<EventParticipation>();
    public ICollection<EventReport> Reports { get; set; } = new List<EventReport>();
}
