using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Infrastructure.Data;

namespace SporcuGelisim.Infrastructure.Services;

public sealed class AthleteProfileService(ApplicationDbContext db, IAthleteAccessService access) : IAthleteProfileService
{
    public async Task<AthleteProfileDto> GetAsync(Guid athleteProfileId, CancellationToken cancellationToken)
    {
        await access.EnsureCanAccessAthleteAsync(athleteProfileId, cancellationToken);
        return await ProjectProfiles(athleteProfileId).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sporcu profili bulunamadı.");
    }

    public async Task<AthleteProfileDto> UpdateAsync(UpdateAthleteProfileRequest request, CancellationToken cancellationToken)
    {
        await access.EnsureCanAccessAthleteAsync(request.AthleteProfileId, cancellationToken);
        if (request.PrimaryBranchId.HasValue)
        {
            var branchActive = await db.SportBranches.AnyAsync(x => x.Id == request.PrimaryBranchId && x.IsActive, cancellationToken);
            if (!branchActive)
            {
                throw new ConflictException("Pasif branş sporcuya atanamaz.");
            }
        }

        var profile = await db.AthleteProfiles.FirstOrDefaultAsync(x => x.Id == request.AthleteProfileId, cancellationToken)
            ?? throw new NotFoundException("Sporcu profili bulunamadı.");

        profile.PrimaryBranchId = request.PrimaryBranchId;
        profile.NationalIdentityNumber = Clean(request.NationalIdentityNumber);
        profile.PhoneNumber = Clean(request.PhoneNumber);
        profile.SecondaryPhoneNumber = Clean(request.SecondaryPhoneNumber);
        profile.ParentPhoneNumber = Clean(request.ParentPhoneNumber);
        profile.Address = Clean(request.Address);
        profile.Biography = Clean(request.Biography);
        profile.BirthDate = request.BirthDate;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(profile.Id, cancellationToken);
    }

    private IQueryable<AthleteProfileDto> ProjectProfiles(Guid? athleteProfileId = null) =>
        from p in db.AthleteProfiles.AsNoTracking().Where(x => !athleteProfileId.HasValue || x.Id == athleteProfileId.Value)
        join u in db.Users.AsNoTracking() on p.UserId equals u.Id
        join b in db.SportBranches.AsNoTracking() on p.PrimaryBranchId equals b.Id into branchJoin
        from branch in branchJoin.DefaultIfEmpty()
        join photoAsset in db.FileAssets.AsNoTracking() on u.ProfilePhotoId equals photoAsset.Id into photoJoin
        from photo in photoJoin.DefaultIfEmpty()
        select new AthleteProfileDto(
            p.Id,
            p.UserId,
            (u.FirstName + " " + u.LastName).Trim(),
            p.PrimaryBranchId,
            branch == null ? null : branch.Name,
            p.NationalIdentityNumber,
            p.PhoneNumber,
            p.SecondaryPhoneNumber,
            p.ParentPhoneNumber,
            p.Address,
            p.Biography,
            p.BirthDate,
            photo == null ? null : photo.RelativePath);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class AthleteRelationService(ApplicationDbContext db, ICurrentUserService currentUser, IAuditService audit) : IAthleteRelationService
{
    public async Task<AthleteRelationDto> AssignAsync(AssignAthleteRelationRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.Roles.Contains(RoleNames.Admin))
        {
            throw new ForbiddenException("İlişki yönetimi yalnızca Admin yetkisindedir.");
        }

        var exists = await db.AthleteRelations.AnyAsync(x =>
            x.AthleteProfileId == request.AthleteProfileId &&
            x.RelatedUserId == request.RelatedUserId &&
            x.RelationType == request.RelationType &&
            x.IsActive, cancellationToken);
        if (exists)
        {
            throw new ConflictException("Aynı kullanıcı ile aktif ilişki zaten var.");
        }

        var entity = new AthleteRelation
        {
            AthleteProfileId = request.AthleteProfileId,
            RelatedUserId = request.RelatedUserId,
            RelationType = request.RelationType,
            CreatedByUserId = currentUser.UserId
        };

        db.AthleteRelations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("AthleteRelationAssigned", nameof(AthleteRelation), entity.Id.ToString(), null, entity, cancellationToken);
        return new AthleteRelationDto(entity.Id, entity.AthleteProfileId, entity.RelatedUserId, entity.RelationType, entity.IsActive);
    }

    public async Task<IReadOnlyList<AthleteProfileDto>> GetRelatedAthletesAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            return [];
        }

        var userId = currentUser.UserId.Value;
        if (currentUser.Roles.Contains(RoleNames.Admin))
        {
            return await (
                from profile in db.AthleteProfiles.AsNoTracking()
                join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
                join branch in db.SportBranches.AsNoTracking() on profile.PrimaryBranchId equals branch.Id into branchJoin
                from branch in branchJoin.DefaultIfEmpty()
                join photoAsset in db.FileAssets.AsNoTracking() on user.ProfilePhotoId equals photoAsset.Id into photoJoin
                from photo in photoJoin.DefaultIfEmpty()
                orderby user.FirstName, user.LastName
                select new AthleteProfileDto(
                    profile.Id,
                    profile.UserId,
                    (user.FirstName + " " + user.LastName).Trim(),
                    profile.PrimaryBranchId,
                    branch == null ? null : branch.Name,
                    profile.NationalIdentityNumber,
                    profile.PhoneNumber,
                    profile.SecondaryPhoneNumber,
                    profile.ParentPhoneNumber,
                    profile.Address,
                    profile.Biography,
                    profile.BirthDate,
                    photo == null ? null : photo.RelativePath))
                .ToListAsync(cancellationToken);
        }

        var query =
            from relation in db.AthleteRelations.AsNoTracking()
            join profile in db.AthleteProfiles.AsNoTracking() on relation.AthleteProfileId equals profile.Id
            join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
            join branch in db.SportBranches.AsNoTracking() on profile.PrimaryBranchId equals branch.Id into branchJoin
            from branch in branchJoin.DefaultIfEmpty()
            join photoAsset in db.FileAssets.AsNoTracking() on user.ProfilePhotoId equals photoAsset.Id into photoJoin
            from photo in photoJoin.DefaultIfEmpty()
            where relation.RelatedUserId == userId && relation.IsActive
            orderby user.FirstName, user.LastName
            select new AthleteProfileDto(
                profile.Id,
                profile.UserId,
                (user.FirstName + " " + user.LastName).Trim(),
                profile.PrimaryBranchId,
                branch == null ? null : branch.Name,
                profile.NationalIdentityNumber,
                profile.PhoneNumber,
                profile.SecondaryPhoneNumber,
                profile.ParentPhoneNumber,
                profile.Address,
                profile.Biography,
                profile.BirthDate,
                photo == null ? null : photo.RelativePath);

        return await query.ToListAsync(cancellationToken);
    }
}
