using SporcuGelisim.Domain.Common;

namespace SporcuGelisim.Domain.Entities;

public sealed class MotivationWord : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentWordId { get; set; }
    public bool IsGlobal { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
    public string CreatedByRole { get; set; } = string.Empty;
}
