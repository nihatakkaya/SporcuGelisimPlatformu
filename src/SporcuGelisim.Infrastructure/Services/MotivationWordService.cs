using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Infrastructure.Data;

namespace SporcuGelisim.Infrastructure.Services;

public sealed class MotivationWordService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    IAthleteAccessService access,
    IAuditService audit) : IMotivationWordService
{
    public async Task<MotivationWordDto> CreateAsync(CreateMotivationWordRequest request, CancellationToken cancellationToken)
    {
        EnsureCanCreate(request.IsGlobal);
        await EnsureUniqueAsync(request.Text, null, cancellationToken);
        await EnsureNoCycleAsync(Guid.Empty, request.ParentWordId, cancellationToken);

        var role = currentUser.Roles.Contains(RoleNames.Admin) ? RoleNames.Admin : RoleNames.Coach;
        var entity = new MotivationWord
        {
            Text = request.Text.Trim(),
            NormalizedText = Normalize(request.Text),
            Description = Clean(request.Description),
            ParentWordId = request.ParentWordId,
            IsGlobal = currentUser.Roles.Contains(RoleNames.Admin) && request.IsGlobal,
            IsActive = true,
            CreatedByUserId = currentUser.UserId,
            CreatedByRole = role
        };

        db.MotivationWords.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await ReplaceBranchesAsync(entity.Id, request.BranchIds, cancellationToken);
        await audit.WriteAsync("WordCreated", nameof(MotivationWord), entity.Id.ToString(), null, entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<MotivationWordDto> UpdateAsync(UpdateMotivationWordRequest request, CancellationToken cancellationToken)
    {
        await access.EnsureCanManageWordAsync(request.Id, cancellationToken);
        var entity = await db.MotivationWords.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Motivasyon kelimesi bulunamadı.");

        if (!currentUser.Roles.Contains(RoleNames.Admin) && request.IsGlobal)
        {
            throw new ForbiddenException("Global kelime işaretleme yalnızca Admin yetkisindedir.");
        }

        await EnsureUniqueAsync(request.Text, request.Id, cancellationToken);
        await EnsureNoCycleAsync(request.Id, request.ParentWordId, cancellationToken);

        entity.Text = request.Text.Trim();
        entity.NormalizedText = Normalize(request.Text);
        entity.Description = Clean(request.Description);
        entity.ParentWordId = request.ParentWordId;
        entity.IsGlobal = currentUser.Roles.Contains(RoleNames.Admin) && request.IsGlobal;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await ReplaceBranchesAsync(entity.Id, request.BranchIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("WordUpdated", nameof(MotivationWord), entity.Id.ToString(), null, entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task AssignBranchesAsync(AssignWordToBranchesRequest request, CancellationToken cancellationToken)
    {
        await access.EnsureCanManageWordAsync(request.MotivationWordId, cancellationToken);
        await ReplaceBranchesAsync(request.MotivationWordId, request.BranchIds, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignAthletesAsync(AssignWordToAthletesRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("Kelime atamak için oturum açmanız gerekir.");
        }

        await access.EnsureCanManageWordAsync(request.MotivationWordId, cancellationToken);

        foreach (var athleteProfileId in request.AthleteProfileIds.Distinct())
        {
            await access.EnsureCanAccessAthleteAsync(athleteProfileId, cancellationToken);
            var exists = await db.AthleteWordAssignments.AnyAsync(x =>
                x.AthleteProfileId == athleteProfileId &&
                x.MotivationWordId == request.MotivationWordId &&
                x.IsActive, cancellationToken);
            if (exists)
            {
                continue;
            }

            db.AthleteWordAssignments.Add(new AthleteWordAssignment
            {
                AthleteProfileId = athleteProfileId,
                MotivationWordId = request.MotivationWordId,
                AssignedByUserId = currentUser.UserId.Value
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAthleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var entity = await db.AthleteWordAssignments.FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken)
            ?? throw new NotFoundException("Kelime ataması bulunamadı.");

        if (!currentUser.Roles.Contains(RoleNames.Admin) && entity.AssignedByUserId != currentUser.UserId)
        {
            throw new ForbiddenException("Yalnızca kendi kelime atamalarınızı kaldırabilirsiniz.");
        }

        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MotivationWordDto>> GetForBranchAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        var query = db.MotivationWords.AsNoTracking().Where(x => x.IsActive);

        if (currentUser.Roles.Contains(RoleNames.Coach) && currentUser.UserId is not null)
        {
            query = query.Where(x => x.IsGlobal || x.CreatedByUserId == currentUser.UserId);
        }

        if (branchId.HasValue)
        {
            var branchIds = await GetBranchLineageAsync(branchId.Value, cancellationToken);
            query = query.Where(x => x.IsGlobal || db.WordBranches.Any(wb => wb.MotivationWordId == x.Id && branchIds.Contains(wb.SportBranchId)));
        }

        return await query.OrderBy(x => x.Text).Select(x => ToDto(x)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WordAssignmentDto>> GetAthleteAssignmentsAsync(CancellationToken cancellationToken)
    {
        var query =
            from assignment in db.AthleteWordAssignments.AsNoTracking()
            join word in db.MotivationWords.AsNoTracking() on assignment.MotivationWordId equals word.Id
            join profile in db.AthleteProfiles.AsNoTracking() on assignment.AthleteProfileId equals profile.Id
            join user in db.Users.AsNoTracking() on profile.UserId equals user.Id
            where assignment.IsActive
            select new { assignment, word, user };

        if (!currentUser.Roles.Contains(RoleNames.Admin))
        {
            query = query.Where(x => x.assignment.AssignedByUserId == currentUser.UserId);
        }

        return await query
            .OrderBy(x => x.user.FirstName)
            .ThenBy(x => x.user.LastName)
            .ThenBy(x => x.word.Text)
            .Select(x => new WordAssignmentDto(
                x.assignment.Id,
                x.word.Id,
                x.word.Text,
                x.assignment.AthleteProfileId,
                (x.user.FirstName + " " + x.user.LastName).Trim(),
                x.assignment.IsActive))
            .ToListAsync(cancellationToken);
    }

    private void EnsureCanCreate(bool requestedGlobal)
    {
        if (currentUser.Roles.Contains(RoleNames.Admin))
        {
            return;
        }

        if (!currentUser.Roles.Contains(RoleNames.Coach))
        {
            throw new ForbiddenException("Kelime ekleme yetkiniz yok.");
        }

        if (requestedGlobal)
        {
            throw new ForbiddenException("Antrenör global kelime oluşturamaz.");
        }
    }

    private async Task EnsureUniqueAsync(string text, Guid? excludedId, CancellationToken cancellationToken)
    {
        var normalized = Normalize(text);
        var exists = await db.MotivationWords.AnyAsync(x => x.NormalizedText == normalized && (!excludedId.HasValue || x.Id != excludedId), cancellationToken);
        if (exists)
        {
            throw new ConflictException("Aynı motivasyon kelimesi zaten var.");
        }
    }

    private async Task EnsureNoCycleAsync(Guid wordId, Guid? parentId, CancellationToken cancellationToken)
    {
        if (wordId != Guid.Empty && wordId == parentId)
        {
            throw new ConflictException("Bir kelime kendisinin üst kelimesi olamaz.");
        }

        var visited = wordId == Guid.Empty ? [] : new HashSet<Guid> { wordId };
        var currentParent = parentId;
        for (var depth = 0; currentParent.HasValue && depth < 32; depth++)
        {
            if (!visited.Add(currentParent.Value))
            {
                throw new ConflictException("Kelime hiyerarşisinde döngü oluşturulamaz.");
            }

            currentParent = await db.MotivationWords.AsNoTracking()
                .Where(x => x.Id == currentParent.Value)
                .Select(x => x.ParentWordId)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }

    private async Task ReplaceBranchesAsync(Guid wordId, IReadOnlyCollection<Guid> branchIds, CancellationToken cancellationToken)
    {
        var existing = await db.WordBranches.Where(x => x.MotivationWordId == wordId).ToListAsync(cancellationToken);
        db.WordBranches.RemoveRange(existing);

        var activeIds = await db.SportBranches.AsNoTracking()
            .Where(x => branchIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        db.WordBranches.AddRange(activeIds.Distinct().Select(branchId => new WordBranch
        {
            MotivationWordId = wordId,
            SportBranchId = branchId,
            AssignedByUserId = currentUser.UserId
        }));
    }

    private async Task<HashSet<Guid>> GetBranchLineageAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var branchIds = new HashSet<Guid>();
        Guid? current = branchId;
        for (var depth = 0; current.HasValue && depth < 32; depth++)
        {
            branchIds.Add(current.Value);
            current = await db.SportBranches.AsNoTracking()
                .Where(x => x.Id == current.Value)
                .Select(x => x.ParentBranchId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return branchIds;
    }

    public static string Normalize(string value) =>
        value.Trim().ToUpperInvariant()
            .Replace("İ", "I").Replace("Ğ", "G").Replace("Ü", "U").Replace("Ş", "S").Replace("Ö", "O").Replace("Ç", "C");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MotivationWordDto ToDto(MotivationWord entity) =>
        new(entity.Id, entity.Text, entity.Description, entity.ParentWordId, entity.IsGlobal, entity.IsActive, entity.CreatedByUserId);
}
