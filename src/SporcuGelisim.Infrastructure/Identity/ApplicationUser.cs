using Microsoft.AspNetCore.Identity;

namespace SporcuGelisim.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public Guid? ProfilePhotoId { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
