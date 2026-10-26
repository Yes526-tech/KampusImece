using System.ComponentModel.DataAnnotations;

namespace KampusImece.Models.Entities;

public class EventReport
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EventId { get; set; }
    public CleanupEvent Event { get; set; } = null!;

    [Required]
    public Guid ReporterUserId { get; set; }
    public AppUser ReporterUser { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }
    public AppUser? ReviewedByUser { get; set; }

    [MaxLength(500)]
    public string? AdminNotes { get; set; }
}
