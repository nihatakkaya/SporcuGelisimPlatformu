using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Infrastructure.Data;
using SporcuGelisim.Infrastructure.Identity;

namespace SporcuGelisim.Infrastructure.Services;

public sealed class UserManagementService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentUserService currentUser,
    IAuditService audit) : IUserManagementService
{
    public async Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");
        var old = user.IsActive;
        user.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("UserActiveChanged", nameof(ApplicationUser), userId.ToString(), new { IsActive = old }, new { user.IsActive }, cancellationToken);
    }

    public async Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        EnsureAdmin();
        if (!RoleNames.All.Contains(roleName))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["Role"] = ["Geçersiz rol."] });
        }

        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("Kullanıcı bulunamadı.");
        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var result = await userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                throw new ConflictException("Rol atanamadı.");
            }

            await audit.WriteAsync("UserRoleAssigned", nameof(ApplicationUser), userId.ToString(), null, new { Role = roleName }, cancellationToken);
        }
    }

    private void EnsureAdmin()
    {
        if (!currentUser.Roles.Contains(RoleNames.Admin))
        {
            throw new ForbiddenException("Bu işlem yalnızca Admin rolüyle yapılabilir.");
        }
    }
}
