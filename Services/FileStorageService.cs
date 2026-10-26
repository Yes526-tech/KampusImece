namespace KampusImece.Services;

public class FileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileStorageService> _logger;

    // Maksimum dosya boyutu: 15 MB (Mobil kamera çekimleri için genişletildi)
    public const long MaxFileSizeBytes = 15 * 1024 * 1024;

    // İzin verilen dosya uzantıları
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    // İzin verilen dosya imzaları (Magic Bytes)
    private static readonly byte[] JpegMagicBytes = new byte[] { 0xFF, 0xD8 };
    private static readonly byte[] PngMagicBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] RiffHeader = new byte[] { 0x52, 0x49, 0x46, 0x46 }; // "RIFF"
    private static readonly byte[] WebpHeader = new byte[] { 0x57, 0x45, 0x42, 0x50 }; // "WEBP"

    public FileStorageService(IWebHostEnvironment environment, ILogger<FileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> SaveSpotImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Yüklenecek bir dosya seçilmedi.", nameof(file));
        }

        // 1. Boyut Kontrolü (Max 5 MB)
        if (file.Length > MaxFileSizeBytes)
        {
            throw new InvalidOperationException($"Dosya boyutu 5 MB sınırını aşıyor. Yüklenen boyut: {(file.Length / (1024.0 * 1024.0)):F2} MB");
        }

        // 2. Uzantı Kontrolü
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Yalnızca .jpg, .jpeg, .png ve .webp formatındaki resim dosyaları kabul edilir.");
        }

        // 3. File Signature (Magic Bytes) Kontrolü
        using (var stream = file.OpenReadStream())
        {
            if (!IsValidImageSignature(stream, extension))
            {
                _logger.LogWarning("Zararlı dosya yükleme girişimi engellendi! Uzantı: {Extension}, Boyut: {Size} byte", extension, file.Length);
                throw new InvalidOperationException("Dosya içeriği geçerli bir resim formatı ile uyuşmuyor (Magic byte doğrulaması başarısız).");
            }
        }

        // 4. Hedef Dizin Oluşturma (wwwroot/uploads/spots)
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "spots");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        // 5. Güvenli Dosya Adı Oluşturma (Guid.NewGuid() + extension)
        var safeFileName = $"{Guid.NewGuid()}{extension}";
        var physicalPath = Path.Combine(uploadsFolder, safeFileName);

        // 6. Dosyayı Diske Kaydetme
        using (var targetStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(targetStream);
        }

        _logger.LogInformation("Güvenli resim yüklendi: {FileName}", safeFileName);

        // Web üzerinden erişilecek göreceli yol
        return $"/uploads/spots/{safeFileName}";
    }

    public Task<bool> DeleteImageAsync(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return Task.FromResult(false);

        try
        {
            // Sadece /uploads/spots/ altındaki dosyaların silinmesine izin vererek Directory Traversal engellenir
            var fileName = Path.GetFileName(relativePath);
            var physicalPath = Path.Combine(_environment.WebRootPath, "uploads", "spots", fileName);

            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
                return Task.FromResult(true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dosya silinirken hata oluştu: {Path}", relativePath);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Akışın başındaki sihirli baytları (Magic Bytes) kontrol eder
    /// </summary>
    private static bool IsValidImageSignature(Stream stream, string extension)
    {
        stream.Position = 0;
        var headerBytes = new byte[16];
        var bytesRead = stream.Read(headerBytes, 0, headerBytes.Length);
        stream.Position = 0; // Akışı başa sar

        if (bytesRead < 8)
            return false;

        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                // JPEG: FF D8
                return headerBytes[0] == 0xFF && headerBytes[1] == 0xD8;

            case ".png":
                // PNG: 89 50 4E 47 0D 0A 1A 0A
                return headerBytes.Take(PngMagicBytes.Length).SequenceEqual(PngMagicBytes);

            case ".webp":
                // WebP: 0..3 'RIFF' ve 8..11 'WEBP'
                if (bytesRead < 12) return false;
                bool isRiff = headerBytes.Take(4).SequenceEqual(RiffHeader);
                bool isWebp = headerBytes.Skip(8).Take(4).SequenceEqual(WebpHeader);
                return isRiff && isWebp;

            default:
                return false;
        }
    }
}
