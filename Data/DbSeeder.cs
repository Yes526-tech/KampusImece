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
        var defaultPasswordHash = passwordHasher.HashPassword("Ogrenci123!");

        if (await context.Users.AnyAsync() || await context.CleanupEvents.AnyAsync())
        {
            // Mevcut veritabanında Yunus Emre SERT ve ysert20051@gmail.com kullanıcısının koordinatör yetkisi garanti altına alınır
            var yunusUser = await context.Users.FirstOrDefaultAsync(u => u.StudentNumber == "233311021" || u.Email == "233311021@ogr.selcuk.edu.tr");
            if (yunusUser != null && !yunusUser.IsCoordinator)
            {
                yunusUser.IsCoordinator = true;
                await context.SaveChangesAsync();
            }

            var ysertEmailUser = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == "ysert20051@gmail.com");
            if (ysertEmailUser == null)
            {
                var newAdminUser = new AppUser
                {
                    Id = Guid.NewGuid(),
                    FullName = "Yunus Emre SERT",
                    Email = "ysert20051@gmail.com",
                    PasswordHash = defaultPasswordHash,
                    StudentNumber = "233311021A",
                    PhoneNumber = "05423884239",
                    ReliabilityScore = 100,
                    IsCoordinator = true,
                    CreatedAt = DateTime.UtcNow,
                    IsKvkkApproved = true,
                    KvkkApprovedAt = DateTime.UtcNow,
                    KvkkVersion = "v1.0"
                };
                context.Users.Add(newAdminUser);
                await context.SaveChangesAsync();
            }
            else if (!ysertEmailUser.IsCoordinator)
            {
                ysertEmailUser.IsCoordinator = true;
                await context.SaveChangesAsync();
            }

            return;
        }

        // 1. ÖĞRENCİ KULLANICILARI (Proje Teklif Formundaki Gerçek Ekip + Gönüllüler)
        var userHuseyin = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = "Hüseyin GÜLCAN",
            Email = "233311070@ogr.selcuk.edu.tr",
            PasswordHash = defaultPasswordHash,
            StudentNumber = "233311070",
            PhoneNumber = "05550794944",
            ReliabilityScore = 100,
            IsCoordinator = true, // Proje Koordinatörü
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            IsKvkkApproved = true,
            KvkkApprovedAt = DateTime.UtcNow.AddMonths(-3),
            KvkkVersion = "v1.0"
        };

        var userYunus = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = "Yunus Emre SERT",
            Email = "233311021@ogr.selcuk.edu.tr",
            PasswordHash = defaultPasswordHash,
            StudentNumber = "233311021",
            PhoneNumber = "05423884239",
            ReliabilityScore = 100,
            IsCoordinator = true, // Proje Koordinatörü
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            IsKvkkApproved = true,
            KvkkApprovedAt = DateTime.UtcNow.AddMonths(-3),
            KvkkVersion = "v1.0"
        };

        var userTalha = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = "Muhammed Talha DUMAN",
            Email = "233311023@ogr.selcuk.edu.tr",
            PasswordHash = defaultPasswordHash,
            StudentNumber = "233311023",
            PhoneNumber = "05378166838",
            ReliabilityScore = 95,
            CreatedAt = DateTime.UtcNow.AddMonths(-2),
            IsKvkkApproved = true,
            KvkkApprovedAt = DateTime.UtcNow.AddMonths(-2),
            KvkkVersion = "v1.0"
        };

        var userOmer = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = "Ömer Faruk BAKİ",
            Email = "233311081@ogr.selcuk.edu.tr",
            PasswordHash = defaultPasswordHash,
            StudentNumber = "233311081",
            PhoneNumber = "05455218991",
            ReliabilityScore = 95,
            CreatedAt = DateTime.UtcNow.AddMonths(-2),
            IsKvkkApproved = true,
            KvkkApprovedAt = DateTime.UtcNow.AddMonths(-2),
            KvkkVersion = "v1.0"
        };

        var userAyse = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = "Ayşe Kaya",
            Email = "ayse.kaya@ogr.selcuk.edu.tr",
            PasswordHash = defaultPasswordHash,
            StudentNumber = "233311099",
            PhoneNumber = "05551112233",
            ReliabilityScore = 100,
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            IsKvkkApproved = true,
            KvkkApprovedAt = DateTime.UtcNow.AddMonths(-1),
            KvkkVersion = "v1.0"
        };

        var userAhmet = new AppUser
        {
            Id = Guid.NewGuid(),
            FullName = "Ahmet Yılmaz",
            Email = "ahmet.yilmaz@ogr.selcuk.edu.tr",
            PasswordHash = defaultPasswordHash,
            StudentNumber = "233311098",
            PhoneNumber = "05552223344",
            ReliabilityScore = 90,
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            IsKvkkApproved = true,
            KvkkApprovedAt = DateTime.UtcNow.AddMonths(-1),
            KvkkVersion = "v1.0"
        };

        var users = new List<AppUser> { userHuseyin, userYunus, userTalha, userOmer, userAyse, userAhmet };
        foreach (var u in users)
        {
            u.KvkkConsentHash = passwordHasher.ComputeKvkkAuditHash(u.Id, u.Email, u.StudentNumber, u.KvkkApprovedAt, u.KvkkVersion);
        }
        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();

        // 2. KONYA ALAADDİN KEYKUBAT KAMPÜSÜ KİRLİLİK NOKTALARI (ETKİNLİKLER)
        // 1. Etkinlik: TAMAMLANMIŞ VE KANITLANMIŞ (BEFORE / AFTER FOTOĞRAFLI) ÖRNEK
        var event1 = new CleanupEvent
        {
            Id = Guid.NewGuid(),
            Title = "Teknoloji Fakültesi Arkası Çöp Birikintisi",
            Description = "Teknoloji Fakültesi arkasındaki ağaçlık alanda atılmış plastik şişe, karton bardak ve ambalaj atıkları birikmiş durumda. Bilgisayar Mühendisliği ders çıkışı el birliğiyle temizliyoruz.",
            ImageUrl = "https://images.unsplash.com/photo-1618477461853-cf6ed80faba5?auto=format&fit=crop&w=800&q=80",
            AfterImageUrl = "https://images.unsplash.com/photo-1542601906990-b4d3fb778b09?auto=format&fit=crop&w=800&q=80",
            Latitude = 38.0282,
            Longitude = 32.5088,
            LocationName = "Teknoloji Fakültesi Arkası",
            RequiredVolunteers = 5,
            ScheduledAt = DateTime.UtcNow.AddDays(-1),
            Status = EventStatus.Completed,
            IsApproved = true,
            CreatorId = userHuseyin.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            CompletedByUserId = userHuseyin.Id,
            CompletionNote = "4 poşet plastik ve cam atık toplandı, kampüs sıfır atık geri dönüşüm kutularına teslim edildi."
        };

        var event2 = new CleanupEvent
        {
            Id = Guid.NewGuid(),
            Title = "Yurt Yolu Üzeri Boş Arazi & Tarla Kenarı",
            Description = "KYK yurtlarından fakültelere uzanan yürüyüş yolunun sağ tarafındaki arazide rüzgarla biriken naylon poşetler ve inşaat atıkları bulunuyor. Kontenjan dolmuştur.",
            ImageUrl = "https://images.unsplash.com/photo-1530587191325-3db32d826c18?auto=format&fit=crop&w=800&q=80",
            AfterImageUrl = null,
            Latitude = 38.0315,
            Longitude = 32.5135,
            LocationName = "Yurt Yolu Üzeri Boş Arazi & Tarla Kenarı",
            RequiredVolunteers = 6,
            ScheduledAt = DateTime.UtcNow.AddDays(3),
            Status = EventStatus.QuotaFull,
            IsApproved = true,
            CreatorId = userYunus.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-4)
        };

        var event3 = new CleanupEvent
        {
            Id = Guid.NewGuid(),
            Title = "Gökkuşağı AVM Yürüyüş Yolu Çevresi",
            Description = "Gökkuşağı AVM yanındaki yürüyüş yolu boyunca bankların çevresinde izmaritler ve ambalaj atıkları tespit edildi. Duyarlı kampüs sakinlerini bekliyoruz.",
            ImageUrl = "https://images.unsplash.com/photo-1563245372-f21724e3856d?auto=format&fit=crop&w=800&q=80",
            AfterImageUrl = null,
            Latitude = 38.0242,
            Longitude = 32.5068,
            LocationName = "Gökkuşağı AVM Yürüyüş Yolu Çevresi",
            RequiredVolunteers = 4,
            ScheduledAt = DateTime.UtcNow.AddDays(4),
            Status = EventStatus.Open,
            IsApproved = true,
            CreatorId = userTalha.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var event4 = new CleanupEvent
        {
            Id = Guid.NewGuid(),
            Title = "Mühendislik Kantini Yanı Yeşil Alan",
            Description = "Mühendislik Fakültesi kantin çıkışındaki açık yeşil alanda piknik ve ara öğün atıkları mevcut. Çevre kirliliğini önlemek adına kısa süreli bir temizlik gerçekleştireceğiz.",
            ImageUrl = "https://images.unsplash.com/photo-1618477461853-cf6ed80faba5?auto=format&fit=crop&w=800&q=80",
            AfterImageUrl = null,
            Latitude = 38.0296,
            Longitude = 32.5105,
            LocationName = "Mühendislik Kantini Yanı Yeşil Alan",
            RequiredVolunteers = 3,
            ScheduledAt = DateTime.UtcNow.AddDays(5),
            Status = EventStatus.Open,
            IsApproved = true,
            CreatorId = userOmer.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        var events = new List<CleanupEvent> { event1, event2, event3, event4 };
        await context.CleanupEvents.AddRangeAsync(events);
        await context.SaveChangesAsync();

        // 3. ETKİNLİK KATILIMLARI (PARTICIPATIONS)
        var participations = new List<EventParticipation>
        {
            new() { Id = Guid.NewGuid(), EventId = event1.Id, UserId = userHuseyin.Id, JoinedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = Guid.NewGuid(), EventId = event1.Id, UserId = userYunus.Id, JoinedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = Guid.NewGuid(), EventId = event1.Id, UserId = userTalha.Id, JoinedAt = DateTime.UtcNow.AddDays(-1) },

            new() { Id = Guid.NewGuid(), EventId = event2.Id, UserId = userHuseyin.Id, JoinedAt = DateTime.UtcNow.AddDays(-3) },
            new() { Id = Guid.NewGuid(), EventId = event2.Id, UserId = userYunus.Id, JoinedAt = DateTime.UtcNow.AddDays(-3) },
            new() { Id = Guid.NewGuid(), EventId = event2.Id, UserId = userTalha.Id, JoinedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = Guid.NewGuid(), EventId = event2.Id, UserId = userOmer.Id, JoinedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = Guid.NewGuid(), EventId = event2.Id, UserId = userAyse.Id, JoinedAt = DateTime.UtcNow.AddDays(-1) },
            new() { Id = Guid.NewGuid(), EventId = event2.Id, UserId = userAhmet.Id, JoinedAt = DateTime.UtcNow.AddDays(-1) },

            new() { Id = Guid.NewGuid(), EventId = event3.Id, UserId = userHuseyin.Id, JoinedAt = DateTime.UtcNow.AddDays(-1) },

            new() { Id = Guid.NewGuid(), EventId = event4.Id, UserId = userYunus.Id, JoinedAt = DateTime.UtcNow.AddDays(-1) },
            new() { Id = Guid.NewGuid(), EventId = event4.Id, UserId = userTalha.Id, JoinedAt = DateTime.UtcNow.AddHours(-12) }
        };

        await context.EventParticipations.AddRangeAsync(participations);
        await context.SaveChangesAsync();
    }
}
