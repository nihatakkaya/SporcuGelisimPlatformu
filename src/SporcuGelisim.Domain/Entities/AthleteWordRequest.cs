using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Enums;

namespace SporcuGelisim.Domain.Entities;

public sealed class AthleteWordRequest : BaseEntity
{
    public Guid AthleteProfileId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid TargetCoachUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string? Note { get; set; }
    public WordRequestStatus Status { get; set; } = WordRequestStatus.Pending;
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? CreatedWordId { get; set; }
}
