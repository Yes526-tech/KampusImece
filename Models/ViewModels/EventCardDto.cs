using KampusImece.Models.Entities;

namespace KampusImece.Models.ViewModels;

public class EventCardDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? AfterImageUrl { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string ScheduledAtFormatted => ScheduledAt.ToString("dd MMMM yyyy, HH:mm");
    public DateTime? CompletedAt { get; set; }
    public string? CompletedAtFormatted => CompletedAt?.ToString("dd MMMM yyyy, HH:mm");
    public Guid? CompletedByUserId { get; set; }
    public string? CompletionNote { get; set; }
    public EventStatus Status { get; set; }
    public string StatusText => Status switch
    {
        EventStatus.Open => "Gönüllü Aranıyor",
        EventStatus.QuotaFull => "Kontenjan Doldu",
        EventStatus.Completed => "Temizlik Tamamlandı",
        EventStatus.Cancelled => "İptal Edildi",
        _ => "Bilinmiyor"
    };

    public int RequiredVolunteers { get; set; }
    public int CurrentParticipantsCount { get; set; }
    public bool IsQuotaFull { get; set; }
    public bool HasCurrentUserJoined { get; set; }
    public bool IsCreator { get; set; }

    /// <summary>
    /// Oturum açmış kullanıcı ilanın kurucusu VEYA katılımcısıysa ve ilan henüz tamamlanmamışsa true döner.
    /// </summary>
    public bool CanCurrentUserComplete { get; set; }

    public string CreatorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Katılımcıların listesi (Ad, Soyad, Baş harfler ve katılım tarihi)
    /// </summary>
    public List<ParticipantSummaryDto> ParticipantsList { get; set; } = new();
}

public class ParticipantSummaryDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public string JoinedAtFormatted => JoinedAt.ToString("dd.MM.yyyy HH:mm");
}
