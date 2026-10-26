namespace KampusImece.Models.Entities;

public enum ReportStatus
{
    Pending = 0,    // İnceleniyor / Beklemede
    Resolved = 1,   // Çözüldü / İlan Kaldırıldı
    Dismissed = 2   // Reddedildi / İhlal Bulunmadı
}
