using SporcuGelisim.Domain.Common;

namespace SporcuGelisim.Domain.Entities;

public sealed class FileAsset : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string RelativePath { get; set; } = string.Empty;
}
