using SporcuGelisim.Domain.Common;

namespace SporcuGelisim.Domain.Entities;

public sealed class Feedback : BaseEntity
{
    public Guid AthleteProfileId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid AuthorUserId { get; set; }
    public Guid? RecipientUserId { get; set; }
    public DateTimeOffset FeedbackDate { get; set; } = DateTimeOffset.UtcNow;
    public string Comment { get; set; } = string.Empty;
}
