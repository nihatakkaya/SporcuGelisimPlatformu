using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Enums;

namespace SporcuGelisim.Domain.Entities;

public sealed class SessionWord : BaseEntity
{
    public Guid SessionId { get; set; }
    public Guid MotivationWordId { get; set; }
    public Guid? SelectedByUserId { get; set; }
    public string SelectedByRole { get; set; } = string.Empty;
    public SelectionSource SelectionSource { get; set; }
    public Guid? CopiedFromSessionWordId { get; set; }
    public string WordTextSnapshot { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
