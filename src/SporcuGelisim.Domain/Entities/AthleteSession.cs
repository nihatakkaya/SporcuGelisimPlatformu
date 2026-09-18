using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Enums;

namespace SporcuGelisim.Domain.Entities;

public sealed class AthleteSession : BaseEntity
{
    public Guid AthleteProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int SessionNumber { get; set; }
    public DateTimeOffset SessionDate { get; set; } = DateTimeOffset.UtcNow;
    public string? Description { get; set; }
    public string? PrivateCoachNote { get; set; }
    public string? SharedNote { get; set; }
    public bool HasWordHistory { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Draft;
    public Guid? CreatedByUserId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
