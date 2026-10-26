using KampusImece.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KampusImece.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CleanupEvent> CleanupEvents => Set<CleanupEvent>();
    public DbSet<EventParticipation> EventParticipations => Set<EventParticipation>();
    public DbSet<EventReport> EventReports => Set<EventReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AppUser Configuration
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.FullName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(150);

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired();

            entity.Property(u => u.StudentNumber)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(u => u.PhoneNumber)
                .HasMaxLength(20)
                .IsRequired(false);

            entity.Property(u => u.ReliabilityScore)
                .HasDefaultValue(100);

            entity.Property(u => u.IsCoordinator)
                .HasDefaultValue(false);

            entity.Property(u => u.CreatedAt)
                .IsRequired();

            entity.Property(u => u.IsKvkkApproved)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(u => u.KvkkApprovedAt)
                .IsRequired();

            entity.Property(u => u.KvkkVersion)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("v1.0");

            entity.Property(u => u.KvkkConsentHash)
                .HasMaxLength(128)
                .IsRequired(false);
        });

        // CleanupEvent Configuration
        modelBuilder.Entity<CleanupEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500);

            entity.Property(e => e.AfterImageUrl)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.Property(e => e.LocationName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Latitude)
                .IsRequired();

            entity.Property(e => e.Longitude)
                .IsRequired();

            entity.Property(e => e.RequiredVolunteers)
                .IsRequired();

            entity.Property(e => e.ScheduledAt)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.IsApproved)
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.CompletedAt)
                .IsRequired(false);

            entity.Property(e => e.CompletedByUserId)
                .IsRequired(false);

            entity.Property(e => e.CompletionNote)
                .HasMaxLength(500)
                .IsRequired(false);

            // İlişki: Bir etkinliği bir kullanıcı oluşturur (Cascade: Kullanıcı sistemden çıkarıldığında etkinlikleri ve katılımları silinir)
            entity.HasOne(e => e.Creator)
                .WithMany(u => u.CreatedEvents)
                .HasForeignKey(e => e.CreatorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // EventParticipation Configuration
        modelBuilder.Entity<EventParticipation>(entity =>
        {
            entity.HasKey(p => p.Id);

            // İlişki: Etkinlik silinirse katılımlar da silinir (Cascade)
            entity.HasOne(p => p.Event)
                .WithMany(e => e.Participations)
                .HasForeignKey(p => p.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // İlişki: Kullanıcı silinirse katılım kayıtları silinir (Cascade)
            entity.HasOne(p => p.User)
                .WithMany(u => u.Participations)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // UNIQUE COMPOSITE INDEX: Bir kullanıcı aynı etkinliğe sadece 1 kez katılabilir
            entity.HasIndex(p => new { p.EventId, p.UserId })
                .IsUnique();
        });

        // EventReport Configuration
        modelBuilder.Entity<EventReport>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Reason)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(r => r.Description)
                .HasMaxLength(1000)
                .IsRequired(false);

            entity.Property(r => r.Status)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(r => r.CreatedAt)
                .IsRequired();

            entity.Property(r => r.AdminNotes)
                .HasMaxLength(500)
                .IsRequired(false);

            entity.HasOne(r => r.Event)
                .WithMany(e => e.Reports)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.ReporterUser)
                .WithMany(u => u.SubmittedReports)
                .HasForeignKey(r => r.ReporterUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(r => r.ReviewedByUser)
                .WithMany()
                .HasForeignKey(r => r.ReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
