namespace KampusImece.Models.ViewModels;

using KampusImece.Models.Entities;

public class UserHistoryViewModel
{
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public int CreatedEventsCount { get; set; }
    public int CompletedCleanupsCount { get; set; }
    public int ActiveEventsCount { get; set; }
    public int TotalParticipationsCount { get; set; }
    public int TotalScore { get; set; }
    public string BadgeTitle { get; set; } = "Yeni Üye";
    public string BadgeColorClass => BadgeTitle switch
    {
        "Kampüs Muhafızı" => "bg-emerald-100 text-emerald-800 border-emerald-300 dark:bg-emerald-950/60 dark:text-emerald-300 dark:border-emerald-800",
        "Yeşil Öncü" => "bg-teal-100 text-teal-800 border-teal-300 dark:bg-teal-950/60 dark:text-teal-300 dark:border-teal-800",
        "Çevre Gönüllüsü" => "bg-blue-100 text-blue-800 border-blue-300 dark:bg-blue-950/60 dark:text-blue-300 dark:border-blue-800",
        _ => "bg-slate-100 text-slate-700 border-slate-300 dark:bg-slate-800 dark:text-slate-300 dark:border-slate-700"
    };
    public string BadgeIcon => BadgeTitle switch
    {
        "Kampüs Muhafızı" => "shield-check",
        "Yeşil Öncü" => "trees",
        "Çevre Gönüllüsü" => "heart-handshake",
        _ => "sprout"
    };

    public List<CleanupEvent> MyCreatedEvents { get; set; } = new();
    public List<CleanupEvent> MyParticipations { get; set; } = new();
}
