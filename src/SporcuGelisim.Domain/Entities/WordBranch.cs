namespace SporcuGelisim.Domain.Entities;

public sealed class WordBranch
{
    public Guid MotivationWordId { get; set; }
    public Guid SportBranchId { get; set; }
    public Guid? AssignedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
