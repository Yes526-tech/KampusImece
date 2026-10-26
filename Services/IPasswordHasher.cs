namespace KampusImece.Services;

public interface IPasswordHasher
{
    /// <summary>
    /// Düz metin parolayı PBKDF2 (SHA-256) ve rastgele Salt ile özetler.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verilen parolanın saklanan hash ile eşleşip eşleşmediğini zamanlama saldırılarına dayanıklı (fixed-time) kontrol eder.
    /// </summary>
    bool VerifyPassword(string password, string passwordHash);

    /// <summary>
    /// 6698 Sayılı KVKK onayının değiştirilemez kriptografik özetini (SHA-256 Denetim İzi) üretir.
    /// </summary>
    string ComputeKvkkAuditHash(Guid userId, string email, string studentNumber, DateTime approvedAt, string version);
}
