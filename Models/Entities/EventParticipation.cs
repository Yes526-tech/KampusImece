namespace KampusImece.Models.Entities;

public class EventParticipation
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }
    public CleanupEvent Event { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
