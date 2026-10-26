namespace KampusImece.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Güvenli dosya doğrulama (uzantı, magic bytes, 5MB sınır) yaparak dosyayı wwwroot/uploads/spots/ dizinine kaydeder.
    /// </summary>
    /// <param name="file">Yüklenen IFormFile nesnesi</param>
    /// <returns>Veritabanında ve HTML src içinde kullanılacak göreceli web yolu (/uploads/spots/{guid}.ext)</returns>
    Task<string> SaveSpotImageAsync(IFormFile file);

    /// <summary>
    /// Belirtilen göreceli dosya yolundaki resmi diskten siler.
    /// </summary>
    Task<bool> DeleteImageAsync(string? relativePath);
}
