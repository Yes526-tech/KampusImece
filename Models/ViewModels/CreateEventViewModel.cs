using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace KampusImece.Models.ViewModels;

public class CreateEventViewModel
{
    [Required(ErrorMessage = "Etkinlik başlığı zorunludur.")]
    [MaxLength(150, ErrorMessage = "Başlık en fazla 150 karakter olabilir.")]
    [Display(Name = "Etkinlik Başlığı")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Açıklama alanı zorunludur.")]
    [MaxLength(1000, ErrorMessage = "Açıklama en fazla 1000 karakter olabilir.")]
    [Display(Name = "Açıklama ve Detaylar")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Konum adı zorunludur.")]
    [MaxLength(200, ErrorMessage = "Konum adı en fazla 200 karakter olabilir.")]
    [Display(Name = "Konum Adı (Örn: Teknoloji Fakültesi Arkası)")]
    public string LocationName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Haritadan enlem (latitude) seçilmelidir.")]
    [Display(Name = "Enlem (Latitude)")]
    public string Latitude { get; set; } = "38.0260"; // Kampüs varsayılanı

    [Required(ErrorMessage = "Haritadan boylam (longitude) seçilmelidir.")]
    [Display(Name = "Boylam (Longitude)")]
    public string Longitude { get; set; } = "32.5090"; // Kampüs varsayılanı

    [Required(ErrorMessage = "Gönüllü kotası belirtilmelidir.")]
    [Range(1, 50, ErrorMessage = "Gönüllü sayısı en az 1, en fazla 50 kişi olabilir.")]
    [Display(Name = "Gerekli Gönüllü Sayısı (Kota)")]
    public int RequiredVolunteers { get; set; } = 5;

    [Required(ErrorMessage = "Temizlik buluşma tarihi ve saati seçilmelidir.")]
    [FutureDate(minimumMinutesAhead: 15, ErrorMessage = "Buluşma zamanı gelecekte en az 15 dakika sonrası olmalıdır.")]
    [Display(Name = "Buluşma Tarihi ve Saati")]
    public DateTime ScheduledAt { get; set; } = DateTime.Now.AddDays(1);

    [Required(ErrorMessage = "Kirlilik noktasının fotoğrafını yüklemek zorunludur.")]
    [Display(Name = "Kirlilik Noktası Fotoğrafı (Öncesi)")]
    public IFormFile Photo { get; set; } = null!;

    [Display(Name = "Temizlenmiş Alan Fotoğrafı (Sonrası - İsteğe Bağlı)")]
    public IFormFile? AfterPhoto { get; set; }

    /// <summary>
    /// Kültür (Culture) ayarlarından bağımsız olarak InvariantCulture ile parse edilmiş enlem
    /// </summary>
    public double ParsedLatitude => ParseCoordinate(Latitude, 38.0260);

    /// <summary>
    /// Kültür (Culture) ayarlarından bağımsız olarak InvariantCulture ile parse edilmiş boylam
    /// </summary>
    public double ParsedLongitude => ParseCoordinate(Longitude, 32.5090);

    /// <summary>
    /// Hem virgül (38,0260) hem nokta (38.0260) formatını InvariantCulture kullanarak güvenle parse eder.
    /// </summary>
    public static double ParseCoordinate(string? value, double fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var normalized = value.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        return fallback;
    }
}
