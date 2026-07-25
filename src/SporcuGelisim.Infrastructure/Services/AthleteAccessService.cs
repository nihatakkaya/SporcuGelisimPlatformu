using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Enums;
using SporcuGelisim.Infrastructure.Data;

namespace SporcuGelisim.Infrastructure.Services;

public sealed class AthleteAccessService(ApplicationDbContext db, ICurrentUserService currentUser) : IAthleteAccessService
{
    public async Task<bool> CanAccessAthleteAsync(Guid athleteProfileId, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return false;
        }

        if (currentUser.Roles.Contains(RoleNames.Admin))
        {
            return true;
        }

        var userId = currentUser.UserId.Value;

        if (currentUser.Roles.Contains(RoleNames.Athlete))
        {
            return await db.AthleteProfiles.AsNoTracking()
                .AnyAsync(x => x.Id == athleteProfileId && x.UserId == userId, cancellationToken);
        }

        if (currentUser.Roles.Contains(RoleNames.Coach) || currentUser.Roles.Contains(RoleNames.Parent))
        {
            var now = DateTimeOffset.UtcNow;
            var candidateRelations = await db.AthleteRelations.AsNoTracking()
                .Where(x => x.AthleteProfileId == athleteProfileId
                    && x.RelatedUserId == userId
                    && x.IsActive)
                .Select(x => x.EndDate)
                .ToListAsync(cancellationToken);
            return candidateRelations.Any(endDate => endDate is null || endDate > now);
        }

        return false;
    }

    public async Task EnsureCanAccessAthleteAsync(Guid athleteProfileId, CancellationToken cancellationToken)
    {
        if (!await CanAccessAthleteAsync(athleteProfileId, cancellationToken))
        {
            throw new ForbiddenException("Bu sporcunun bilgilerine erişim yetkiniz yok.");
        }
    }

    public async Task EnsureCanManageSessionAsync(Guid athleteProfileId, CancellationToken cancellationToken)
    {
        await EnsureCanAccessAthleteAsync(athleteProfileId, cancellationToken);
        if (currentUser.Roles.Contains(RoleNames.Parent))
        {
            throw new ForbiddenException("Ebeveyn kullanıcılar oturum yönetemez.");
        }
    }

    public Task EnsureCanCreateFeedbackAsync(Guid athleteProfileId, CancellationToken cancellationToken) =>
        EnsureCanAccessAthleteAsync(athleteProfileId, cancellationToken);

    public async Task EnsureCanManageWordAsync(Guid wordId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("Oturum açmanız gerekir.");
        }

        if (currentUser.Roles.Contains(RoleNames.Admin))
        {
            return;
        }

        if (!currentUser.Roles.Contains(RoleNames.Coach))
        {
            throw new ForbiddenException("Kelime yönetimi için yetkiniz yok.");
        }

        var ownsWord = await db.MotivationWords.AsNoTracking()
            .AnyAsync(x => x.Id == wordId && x.CreatedByUserId == currentUser.UserId, cancellationToken);
        if (!ownsWord)
        {
            throw new ForbiddenException("Yalnızca kendi eklediğiniz kelimeleri düzenleyebilirsiniz.");
        }
    }
}
