namespace KampusImece.Models.ViewModels;

public class LeaderboardEntryDto
{
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Kullanıcının başlattığı kirlilik ilanı sayısı (+25 Puan)
    /// </summary>
    public int CreatedEventsCount { get; set; }

    /// <summary>
    /// Kullanıcının katıldığı temizlik etkinliği sayısı (+15 Puan)
    /// </summary>
    public int JoinedEventsCount { get; set; }

    /// <summary>
    /// Kullanıcının parçası olduğu ve başarıyla tamamlanıp kanıtlanan temizlik sayısı (+30 Bonus Puan)
    /// </summary>
    public int CompletedEventsCount { get; set; }

    /// <summary>
    /// Kullanıcının bizzat yüklediği tamamlanma kanıt fotoğrafı sayısı (+10 Ekstra Puan)
    /// </summary>
    public int ProofUploadedCount { get; set; }

    /// <summary>
    /// Toplam Puan = (Created * 25) + (Joined * 15) + (Completed * 30) + (Proof * 10)
    /// </summary>
    public int TotalScore { get; set; }

    /// <summary>
    /// Güvenilirlik Puanı (Başlangıç 100)
    /// </summary>
    public int ReliabilityScore { get; set; } = 100;

    public string BadgeTitle { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;

    /// <summary>
    /// Rozet başlığına göre Tailwind CSS stil sınıfları
    /// </summary>
    public string BadgeColorClass => BadgeTitle switch
    {
        "Kampüs Muhafızı" => "bg-emerald-100 text-emerald-800 border-emerald-300",
        "Yeşil Öncü" => "bg-teal-100 text-teal-800 border-teal-300",
        "Çevre Gönüllüsü" => "bg-blue-100 text-blue-800 border-blue-300",
        _ => "bg-slate-100 text-slate-700 border-slate-300"
    };

    /// <summary>
    /// Rozet başlığına göre Lucide ikon adı
    /// </summary>
    public string BadgeIcon => BadgeTitle switch
    {
        "Kampüs Muhafızı" => "shield-check",
        "Yeşil Öncü" => "trees",
        "Çevre Gönüllüsü" => "heart-handshake",
        _ => "sprout"
    };

    /// <summary>
    /// Öğrenci numarasını gizlilik için maskeler (Örn: 233311070 -> 2333****)
    /// </summary>
    public static string MaskStudentNumber(string? studentNumber)
    {
        if (string.IsNullOrWhiteSpace(studentNumber))
            return "******";

        var trimmed = studentNumber.Trim();
        if (trimmed.Length <= 4)
            return trimmed + "****";

        return trimmed[..4] + "****";
    }

    /// <summary>
    /// Toplam puana göre çevre başarı rozetini belirler:
    /// >= 100 Puan: Kampüs Muhafızı
    /// >= 50 Puan: Yeşil Öncü
    /// >= 25 Puan: Çevre Gönüllüsü
    /// &lt; 25 Puan: Yeni Üye
    /// </summary>
    public static string DetermineBadge(int totalScore)
    {
        return totalScore switch
        {
            >= 100 => "Kampüs Muhafızı",
            >= 50 => "Yeşil Öncü",
            >= 25 => "Çevre Gönüllüsü",
            _ => "Yeni Üye"
        };
    }
}
