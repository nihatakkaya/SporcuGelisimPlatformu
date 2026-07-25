using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Enums;

namespace SporcuGelisim.Domain.Entities;

public sealed class AthleteRelation : BaseEntity
{
    public Guid AthleteProfileId { get; set; }
    public Guid RelatedUserId { get; set; }
    public AthleteRelationType RelationType { get; set; }
    public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
}
