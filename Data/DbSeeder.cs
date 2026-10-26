using KampusImece.Models.Entities;
using KampusImece.Services;
using Microsoft.EntityFrameworkCore;

namespace KampusImece.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, IPasswordHasher? passwordHasher = null)
    {
        // Veritabanı tablolarının ve bekleyen migration'ların uygulandığından emin olunur
        if ((await context.Database.GetPendingMigrationsAsync()).Any())
        {
            await context.Database.MigrateAsync();
        }
        else
        {
            await context.Database.EnsureCreatedAsync();
        }

        passwordHasher ??= new PasswordHasher();
        var adminPassword = "Admin123!";
        var adminPasswordHash = passwordHasher.HashPassword(adminPassword);

        // 1. TEKİL YÖNETİCİ (ADMIN / KOORDİNATÖR) HESAPLARI
        var adminEmail = "admin@kampusimece.com";
        var personalEmail = "ysert20051@gmail.com";

        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == adminEmail);
        if (adminUser == null)
        {
            adminUser = new AppUser
            {
                Id = Guid.NewGuid(),
                FullName = "Yunus Emre SERT (Yönetici)",
                Email = adminEmail,
                PasswordHash = adminPasswordHash,
                StudentNumber = "233311021",
                PhoneNumber = "05423884239",
                ReliabilityScore = 100,
                IsCoordinator = true,
                CreatedAt = DateTime.UtcNow,
                IsKvkkApproved = true,
                KvkkApprovedAt = DateTime.UtcNow,
                KvkkVersion = "v1.0"
            };
            adminUser.KvkkConsentHash = passwordHasher.ComputeKvkkAuditHash(adminUser.Id, adminUser.Email, adminUser.StudentNumber, adminUser.KvkkApprovedAt, adminUser.KvkkVersion);
            context.Users.Add(adminUser);
        }
        else
        {
            adminUser.IsCoordinator = true;
            adminUser.PasswordHash = adminPasswordHash;
        }

        var personalUser = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == personalEmail);
        if (personalUser == null)
        {
            personalUser = new AppUser
            {
                Id = Guid.NewGuid(),
                FullName = "Yunus Emre SERT",
                Email = personalEmail,
                PasswordHash = adminPasswordHash,
                StudentNumber = "233311021A",
                PhoneNumber = "05423884239",
                ReliabilityScore = 100,
                IsCoordinator = true,
                CreatedAt = DateTime.UtcNow,
                IsKvkkApproved = true,
                KvkkApprovedAt = DateTime.UtcNow,
                KvkkVersion = "v1.0"
            };
            personalUser.KvkkConsentHash = passwordHasher.ComputeKvkkAuditHash(personalUser.Id, personalUser.Email, personalUser.StudentNumber, personalUser.KvkkApprovedAt, personalUser.KvkkVersion);
            context.Users.Add(personalUser);
        }
        else
        {
            personalUser.IsCoordinator = true;
            personalUser.PasswordHash = adminPasswordHash;
        }

        await context.SaveChangesAsync();

        // 2. KAMPÜS KİRLİLİK NOKTALARI (Başlangıçta haritada açık noktaların bulunması için)
        if (!await context.CleanupEvents.AnyAsync())
        {
            var event1 = new CleanupEvent
            {
                Id = Guid.NewGuid(),
                Title = "Teknoloji Fakültesi Arkası Çöp Birikintisi",
                Description = "Teknoloji Fakültesi arkasındaki ağaçlık alanda atılmış plastik şişe, karton bardak ve ambalaj atıkları birikmiş durumda. El birliğiyle temizliyoruz.",
                ImageUrl = "https://images.unsplash.com/photo-1618477461853-cf6ed80faba5?auto=format&fit=crop&w=800&q=80",
                Latitude = 38.0282,
                Longitude = 32.5088,
                LocationName = "Teknoloji Fakültesi Arkası",
                RequiredVolunteers = 5,
                ScheduledAt = DateTime.UtcNow.AddDays(2),
                Status = EventStatus.Open,
                IsApproved = true,
                CreatorId = adminUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            var event2 = new CleanupEvent
            {
                Id = Guid.NewGuid(),
                Title = "Yurt Yolu Üzeri Boş Arazi & Tarla Kenarı",
                Description = "KYK yurtlarından fakültelere uzanan yürüyüş yolunun sağ tarafındaki arazide rüzgarla biriken naylon poşetler ve inşaat atıkları bulunuyor.",
                ImageUrl = "https://images.unsplash.com/photo-1530587191325-3db32d826c18?auto=format&fit=crop&w=800&q=80",
                Latitude = 38.0315,
                Longitude = 32.5135,
                LocationName = "Yurt Yolu Üzeri Boş Arazi",
                RequiredVolunteers = 6,
                ScheduledAt = DateTime.UtcNow.AddDays(3),
                Status = EventStatus.Open,
                IsApproved = true,
                CreatorId = adminUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            var event3 = new CleanupEvent
            {
                Id = Guid.NewGuid(),
                Title = "Gökkuşağı AVM Yürüyüş Yolu Çevresi",
                Description = "Gökkuşağı AVM yanındaki yürüyüş yolu boyunca bankların çevresinde izmaritler ve ambalaj atıkları tespit edildi. Duyarlı kampüs sakinlerini bekliyoruz.",
                ImageUrl = "https://images.unsplash.com/photo-1563245372-f21724e3856d?auto=format&fit=crop&w=800&q=80",
                Latitude = 38.0242,
                Longitude = 32.5068,
                LocationName = "Gökkuşağı AVM Yürüyüş Yolu",
                RequiredVolunteers = 4,
                ScheduledAt = DateTime.UtcNow.AddDays(4),
                Status = EventStatus.Open,
                IsApproved = true,
                CreatorId = adminUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            var event4 = new CleanupEvent
            {
                Id = Guid.NewGuid(),
                Title = "Mühendislik Kantini Yanı Yeşil Alan",
                Description = "Mühendislik Fakültesi kantin çıkışındaki açık yeşil alanda piknik ve ara öğün atıkları mevcut. Çevre kirliliğini önlemek adına kısa süreli bir temizlik gerçekleştireceğiz.",
                ImageUrl = "https://images.unsplash.com/photo-1618477461853-cf6ed80faba5?auto=format&fit=crop&w=800&q=80",
                Latitude = 38.0296,
                Longitude = 32.5105,
                LocationName = "Mühendislik Kantini Yanı",
                RequiredVolunteers = 3,
                ScheduledAt = DateTime.UtcNow.AddDays(5),
                Status = EventStatus.Open,
                IsApproved = true,
                CreatorId = adminUser.Id,
                CreatedAt = DateTime.UtcNow
            };

            await context.CleanupEvents.AddRangeAsync(event1, event2, event3, event4);
            await context.SaveChangesAsync();
        }
    }
}
