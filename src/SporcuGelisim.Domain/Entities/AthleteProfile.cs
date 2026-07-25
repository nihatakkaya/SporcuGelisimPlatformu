using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Enums;

namespace SporcuGelisim.Domain.Entities;

public sealed class AthleteProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? PrimaryBranchId { get; set; }
    public string? Biography { get; set; }
    public DateOnly? BirthDate { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; } = RegistrationStatus.Active;
}
