using SporcuGelisim.Domain.Common;

namespace SporcuGelisim.Domain.Entities;

public sealed class SessionWordSnapshot : BaseEntity
{
    public Guid SessionId { get; set; }
    public Guid MotivationWordId { get; set; }
    public string WordText { get; set; } = string.Empty;
    public bool IsBeginning { get; set; }
}

public sealed class AthleteWordChange : BaseEntity
{
    public Guid AthleteProfileId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid MotivationWordId { get; set; }
    public string WordText { get; set; } = string.Empty;
    public bool Added { get; set; }
    public string Source { get; set; } = "assigned_words";
    public Guid ChangedByUserId { get; set; }
}
