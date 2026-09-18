using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Domain.Enums;
using SporcuGelisim.Infrastructure.Data;
using SporcuGelisim.Infrastructure.Identity;

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
        foreach (var athleteId in request.AthleteProfileIds.Distinct())
            await access.EnsureCanManageSessionAsync(athleteId, cancellationToken);
        foreach (var athleteId in request.AthleteProfileIds.Distinct())
            await new AthleteWordWorkflow(db, currentUser, access).ChangeAsync(new(athleteId, [request.MotivationWordId], []), cancellationToken);
    }

    public async Task RemoveAthleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken)
    {
        var entity = await db.AthleteWordAssignments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == assignmentId, cancellationToken)
            ?? throw new NotFoundException("Kelime ataması bulunamadı.");
        await new AthleteWordWorkflow(db, currentUser, access).ChangeAsync(new(entity.AthleteProfileId, [], [entity.MotivationWordId]), cancellationToken);
    }
    public async Task<IReadOnlyList<MotivationWordDto>> GetForBranchAsync(Guid? branchId, CancellationToken cancellationToken)
    {
        var query = db.MotivationWords.AsNoTracking().Where(x => x.IsActive);

        if (currentUser.Roles.Contains(RoleNames.Coach) && currentUser.UserId is not null)
        {
            query = query.Where(x => x.IsGlobal || x.CreatedByUserId == currentUser.UserId);
        }

        if (currentUser.Roles.Contains(RoleNames.Athlete) && currentUser.UserId is not null)
        {
            var athleteProfileId = await db.AthleteProfiles.AsNoTracking()
                .Where(x => x.UserId == currentUser.UserId.Value)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            query = athleteProfileId.HasValue
                ? query.Where(x => db.AthleteWordAssignments.Any(a => a.AthleteProfileId == athleteProfileId.Value && a.MotivationWordId == x.Id && a.IsActive))
                : query.Where(_ => false);
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
            select new { assignment, word, profile, user };

        if (currentUser.Roles.Contains(RoleNames.Athlete) && currentUser.UserId is not null)
        {
            query = query.Where(x => x.profile.UserId == currentUser.UserId.Value);
        }
        else if (!currentUser.Roles.Contains(RoleNames.Admin))
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

    public async Task<WordRequestDto> SubmitWordRequestAsync(SubmitWordRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null || !currentUser.Roles.Contains(RoleNames.Athlete))
        {
            throw new ForbiddenException("Kelime isteği yalnızca sporcu hesabıyla gönderilebilir.");
        }

        await access.EnsureCanAccessAthleteAsync(request.AthleteProfileId, cancellationToken);
        var candidateRelations = await db.AthleteRelations.AsNoTracking()
            .Where(x =>
            x.AthleteProfileId == request.AthleteProfileId &&
            x.RelatedUserId == request.TargetCoachUserId &&
            x.RelationType == AthleteRelationType.Coach &&
            x.IsActive)
            .Select(x => x.EndDate)
            .ToListAsync(cancellationToken);
        var isResponsibleCoach = candidateRelations.Any(endDate => endDate is null || endDate > DateTimeOffset.UtcNow);
        if (!isResponsibleCoach)
        {
            throw new ForbiddenException("Kelime isteği yalnızca sorumlu antrenöre gönderilebilir.");
        }

        var normalized = Normalize(request.Text);
        var pendingExists = await db.AthleteWordRequests.AnyAsync(x =>
            x.AthleteProfileId == request.AthleteProfileId &&
            x.TargetCoachUserId == request.TargetCoachUserId &&
            x.NormalizedText == normalized &&
            x.Status == WordRequestStatus.Pending, cancellationToken);
        if (pendingExists)
        {
            throw new ConflictException("Bu kelime için bekleyen bir isteğiniz zaten var.");
        }

        var entity = new AthleteWordRequest
        {
            AthleteProfileId = request.AthleteProfileId,
            RequestedByUserId = currentUser.UserId.Value,
            TargetCoachUserId = request.TargetCoachUserId,
            Text = request.Text.Trim(),
            NormalizedText = normalized,
            Note = Clean(request.Note)
        };
        db.AthleteWordRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("WordRequestSubmitted", nameof(AthleteWordRequest), entity.Id.ToString(), null, entity, cancellationToken);
        return await GetWordRequestAsync(entity.Id, cancellationToken);
    }

    public async Task<WordRequestDto> ReviewWordRequestAsync(ReviewWordRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null || (!currentUser.Roles.Contains(RoleNames.Coach) && !currentUser.Roles.Contains(RoleNames.Admin)))
        {
            throw new ForbiddenException("Kelime isteği yalnızca antrenör tarafından değerlendirilebilir.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            var entity = await db.AthleteWordRequests.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException("Kelime isteği bulunamadı.");
            if (entity.TargetCoachUserId != currentUser.UserId.Value && !currentUser.Roles.Contains(RoleNames.Admin))
            {
                throw new ForbiddenException("Yalnızca size gönderilen kelime isteğini değerlendirebilirsiniz.");
            }

            if (entity.Status != WordRequestStatus.Pending)
            {
                throw new ConflictException("Bu kelime isteği daha önce değerlendirilmiş.");
            }

            await access.EnsureCanManageSessionAsync(entity.AthleteProfileId, cancellationToken);
            entity.Status = request.Approved ? WordRequestStatus.Approved : WordRequestStatus.Rejected;
            entity.ReviewedByUserId = currentUser.UserId.Value;
            entity.ReviewedAt = DateTimeOffset.UtcNow;
            entity.ReviewNote = Clean(request.ReviewNote);
            entity.UpdatedAt = DateTimeOffset.UtcNow;

            if (request.Approved)
            {
                var word = await db.MotivationWords.FirstOrDefaultAsync(x => x.NormalizedText == entity.NormalizedText, cancellationToken);
                if (word is null)
                {
                    word = new MotivationWord
                    {
                        Text = entity.Text,
                        NormalizedText = entity.NormalizedText,
                        Description = entity.Note,
                        IsGlobal = false,
                        IsActive = true,
                        CreatedByUserId = currentUser.UserId.Value,
                        CreatedByRole = RoleNames.Coach
                    };
                    db.MotivationWords.Add(word);
                    await db.SaveChangesAsync(cancellationToken);
                }

                entity.CreatedWordId = word.Id;
                await AssignWordToAthleteAsync(word.Id, entity.AthleteProfileId, currentUser.UserId.Value, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await audit.WriteAsync("WordRequestReviewed", nameof(AthleteWordRequest), entity.Id.ToString(), null, entity, cancellationToken);
            var result = await GetWordRequestAsync(entity.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<IReadOnlyList<WordRequestDto>> GetWordRequestsAsync(CancellationToken cancellationToken)
    {
        var requestQuery = db.AthleteWordRequests.AsNoTracking();
        if (currentUser.Roles.Contains(RoleNames.Athlete) && currentUser.UserId is not null)
        {
            requestQuery = requestQuery.Where(x => x.RequestedByUserId == currentUser.UserId.Value);
        }
        else if (currentUser.Roles.Contains(RoleNames.Coach) && currentUser.UserId is not null)
        {
            requestQuery = requestQuery.Where(x => x.TargetCoachUserId == currentUser.UserId.Value);
        }
        else if (!currentUser.Roles.Contains(RoleNames.Admin))
        {
            return [];
        }

        var rows = await WordRequestRows(requestQuery)
            .OrderByDescending(x => x.Request.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(x => ToWordRequestDto(x.Request, x.AthleteName, x.CoachName)).ToList();
    }

    private async Task<WordRequestDto> GetWordRequestAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await WordRequestRows(db.AthleteWordRequests.AsNoTracking().Where(x => x.Id == id))
            .FirstAsync(cancellationToken);
        return ToWordRequestDto(row.Request, row.AthleteName, row.CoachName);
    }

    private IQueryable<WordRequestProjectionRow> WordRequestRows(IQueryable<AthleteWordRequest> requests) =>
        from request in requests
        join profile in db.AthleteProfiles.AsNoTracking() on request.AthleteProfileId equals profile.Id
        join athleteUser in db.Users.AsNoTracking() on profile.UserId equals athleteUser.Id
        join coachUser in db.Users.AsNoTracking() on request.TargetCoachUserId equals coachUser.Id
        select new WordRequestProjectionRow(
            request,
            (athleteUser.FirstName + " " + athleteUser.LastName).Trim(),
            (coachUser.FirstName + " " + coachUser.LastName).Trim());

    private Task AssignWordToAthleteAsync(Guid motivationWordId, Guid athleteProfileId, Guid assignedByUserId, CancellationToken cancellationToken) =>
        new AthleteWordWorkflow(db, currentUser, access).ChangeAsync(new(athleteProfileId, [motivationWordId], []), cancellationToken);
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
            .Replace("İ", "I")
            .Replace("Ğ", "G")
            .Replace("Ü", "U")
            .Replace("Ş", "S")
            .Replace("Ö", "O")
            .Replace("Ç", "C");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static MotivationWordDto ToDto(MotivationWord entity) =>
        new(entity.Id, entity.Text, entity.Description, entity.ParentWordId, entity.IsGlobal, entity.IsActive, entity.CreatedByUserId);

    private static WordRequestDto ToWordRequestDto(AthleteWordRequest request, string athleteName, string coachName) =>
        new(
            request.Id,
            request.AthleteProfileId,
            athleteName,
            request.TargetCoachUserId,
            coachName,
            request.Text,
            request.Note,
            request.Status,
            request.ReviewNote,
            request.CreatedAt);

    private sealed record WordRequestProjectionRow(AthleteWordRequest Request, string AthleteName, string CoachName);
}
