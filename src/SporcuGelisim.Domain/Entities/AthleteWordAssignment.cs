using SporcuGelisim.Domain.Common;

namespace SporcuGelisim.Domain.Entities;

public sealed class AthleteWordAssignment : BaseEntity
{
    public Guid AthleteProfileId { get; set; }
    public Guid MotivationWordId { get; set; }
    public Guid AssignedByUserId { get; set; }
    public bool IsActive { get; set; } = true;
}
