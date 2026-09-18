using System.Data;
using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Domain.Enums;
using SporcuGelisim.Infrastructure.Data;

namespace SporcuGelisim.Infrastructure.Services;

public sealed partial class AthleteSessionService(ApplicationDbContext db, ICurrentUserService currentUser, IAthleteAccessService access, IAuditService audit)
    : IAthleteSessionService
{
    public async Task<AthleteSessionDto> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken)
    {
        await access.EnsureCanManageSessionAsync(request.AthleteProfileId, cancellationToken);
        if (request.Title?.Length > 160 || request.Description?.Length > 2000)
            throw new ConflictException("Oturum başlığı veya açıklaması çok uzun.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var nextNumber = (await db.AthleteSessions.IgnoreQueryFilters().Where(x => x.AthleteProfileId == request.AthleteProfileId)
                .Select(x => (int?)x.SessionNumber).MaxAsync(cancellationToken) ?? 0) + 1;
            var entity = new AthleteSession
            {
                AthleteProfileId = request.AthleteProfileId, Title = string.IsNullOrWhiteSpace(request.Title) ? $"Oturum {nextNumber}" : request.Title.Trim(),
                SessionDate = new DateTimeOffset(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3)).Date, TimeSpan.FromHours(3)),
                Description = request.Description, SessionNumber = nextNumber, CreatedByUserId = currentUser.UserId,
                Status = SessionStatus.Active, HasWordHistory = true
            };

            db.AthleteSessions.Add(entity);
            AthleteWordWorkflow.AddSnapshots(db, entity.Id, await AthleteWordWorkflow.ReadCurrentAsync(db, request.AthleteProfileId, cancellationToken));
            await db.SaveChangesAsync(cancellationToken);
            await audit.WriteAsync("SessionCreated", nameof(AthleteSession), entity.Id.ToString(), null, new { entity.SessionNumber }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToDto(entity);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<AthleteSessionDto> UpdateAsync(UpdateSessionRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.AthleteSessions.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Oturum bulunamadı.");
        await access.EnsureCanManageSessionAsync(entity.AthleteProfileId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 160 || request.Description?.Length > 2000)
            throw new ConflictException("Oturum başlığı veya açıklaması geçersiz.");
        entity.Title = request.Title.Trim();
        entity.SessionDate = request.SessionDate;
        entity.Description = request.Description;
        entity.Status = request.Status;
        entity.CompletedAt = request.Status == SessionStatus.Completed ? DateTimeOffset.UtcNow : entity.CompletedAt;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SessionUpdated", nameof(AthleteSession), entity.Id.ToString(), null, new { entity.SessionNumber }, cancellationToken);
        return ToDto(entity);
    }

    private async Task EnsureCanReadAsync(Guid athleteId, CancellationToken ct)
    {
        await access.EnsureCanAccessAthleteAsync(athleteId, ct);
        if (currentUser.Roles.Contains(RoleNames.Athlete)) return;
        await access.EnsureCanManageSessionAsync(athleteId, ct);
    }

    public async Task<IReadOnlyList<AthleteSessionDto>> GetForAthleteAsync(Guid athleteProfileId, CancellationToken cancellationToken)
    {
        await EnsureCanReadAsync(athleteProfileId, cancellationToken);
        return await db.AthleteSessions.AsNoTracking().Where(x => x.AthleteProfileId == athleteProfileId)
            .OrderByDescending(x => x.SessionNumber).Select(x => ToDto(x)).ToListAsync(cancellationToken);
    }

    public async Task<SessionDetailDto> GetDetailAsync(Guid athleteProfileId, Guid sessionId, CancellationToken cancellationToken)
    {
        await EnsureCanReadAsync(athleteProfileId, cancellationToken);
        var canManage = !currentUser.Roles.Contains(RoleNames.Athlete) &&
            (currentUser.Roles.Contains(RoleNames.Coach) || currentUser.Roles.Contains(RoleNames.Admin));
        // Private notes are excluded by the database projection for athlete readers.
        var row = await db.AthleteSessions.AsNoTracking().Where(x => x.Id == sessionId && x.AthleteProfileId == athleteProfileId)
            .Select(x => new { Session = new AthleteSessionDto(x.Id, x.AthleteProfileId, x.Title, x.SessionNumber, x.SessionDate, x.Status),
                x.CreatedAt, PrivateNote = canManage ? x.PrivateCoachNote : null, x.SharedNote, x.HasWordHistory, x.CreatedByUserId })
            .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException("Oturum bulunamadı.");
        var snapshots = await db.SessionWordSnapshots.AsNoTracking().Where(x => x.SessionId == sessionId).OrderBy(x => x.WordText).ToListAsync(cancellationToken);
        var changes = await db.AthleteWordChanges.AsNoTracking().Where(x => x.SessionId == sessionId && x.AthleteProfileId == athleteProfileId)
            .Select(x => new WordChangeDto(x.Id, x.MotivationWordId, x.WordText, x.Added, x.Source, x.CreatedAt)).ToListAsync(cancellationToken);
        var beginning = snapshots.Where(x => x.IsBeginning).Select(x => new WordSnapshotDto(x.MotivationWordId, x.WordText)).ToList();
        var ending = snapshots.Where(x => !x.IsBeginning).Select(x => new WordSnapshotDto(x.MotivationWordId, x.WordText)).ToList();
        if (!row.HasWordHistory && snapshots.Count == 0)
        {
            var legacy = await db.SessionWords.AsNoTracking().Where(x => x.SessionId == sessionId).OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
            ending = legacy.Select(x => new WordSnapshotDto(x.MotivationWordId, x.WordTextSnapshot)).ToList();
            foreach (var word in legacy.Where(x => !changes.Any(c => c.MotivationWordId == x.MotivationWordId)))
                changes.Add(new(word.Id, word.MotivationWordId, word.WordTextSnapshot, true, "session", word.CreatedAt));
        }        var result = new SessionDetailDto(row.Session, row.CreatedAt, row.PrivateNote, row.SharedNote, canManage,
            row.HasWordHistory, beginning, ending, changes.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToList(), canManage && (row.CreatedByUserId == currentUser.UserId || currentUser.Roles.Contains(RoleNames.Admin)));
        return result with { Revision = RevisionOf(result) };
    }

    public async Task SaveNotesAsync(SaveSessionNotesRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.AthleteSessions.FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken)
            ?? throw new NotFoundException("Oturum bulunamadı.");
        await access.EnsureCanManageSessionAsync(entity.AthleteProfileId, cancellationToken);
        if (request.PrivateCoachNote?.Length > 10000 || request.SharedNote?.Length > 10000)
            throw new ConflictException("Notlar en fazla 10.000 karakter olabilir.");
        entity.PrivateCoachNote = request.PrivateCoachNote?.Trim();
        entity.SharedNote = request.SharedNote?.Trim();
        entity.SessionDate = new DateTimeOffset(request.SessionDate.Date, TimeSpan.FromHours(3));
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static AthleteSessionDto ToDto(AthleteSession entity) =>
        new(entity.Id, entity.AthleteProfileId, entity.Title, entity.SessionNumber, entity.SessionDate, entity.Status);
}

public sealed class SessionWordService(ApplicationDbContext db, ICurrentUserService currentUser, IAthleteAccessService access, IAuditService audit)
    : ISessionWordService
{
    public async Task<IReadOnlyList<SessionWordDto>> AddWordsAsync(AddSessionWordsRequest request, CancellationToken cancellationToken)
    {
        var session = await db.AthleteSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken)
            ?? throw new NotFoundException("Oturum bulunamadı.");
        await access.EnsureCanManageSessionAsync(session.AthleteProfileId, cancellationToken);
        var activeIds = await db.MotivationWords.Where(x => request.MotivationWordIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync(cancellationToken);
        _ = audit;
        var before = await db.AthleteWordChanges.Where(x => x.SessionId == session.Id).Select(x => x.Id).ToListAsync(cancellationToken);
        await new AthleteWordWorkflow(db, currentUser, access).ChangeAsync(new(session.AthleteProfileId, activeIds, [], session.Id), cancellationToken);
        return await db.AthleteWordChanges.Where(x => x.SessionId == session.Id && x.Added && !before.Contains(x.Id))
            .Select(x => new SessionWordDto(x.Id, session.Id, x.MotivationWordId, x.WordText, SelectionSource.ManuallySelected)).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionWordDto>> CopyWordsAsync(CopySessionWordsRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceSessionId == request.TargetSessionId) throw new ConflictException("Kaynak ve hedef oturum aynı olamaz.");
        var source = await db.AthleteSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.SourceSessionId, cancellationToken) ?? throw new NotFoundException("Kaynak oturum bulunamadı.");
        var target = await db.AthleteSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.TargetSessionId, cancellationToken) ?? throw new NotFoundException("Hedef oturum bulunamadı.");
        if (source.AthleteProfileId != target.AthleteProfileId) throw new ForbiddenException("Başka sporcunun oturumundan kelime kopyalanamaz.");
        await access.EnsureCanManageSessionAsync(target.AthleteProfileId, cancellationToken);
        var ids = await db.SessionWordSnapshots.Where(x => x.SessionId == source.Id && !x.IsBeginning &&
            (request.SessionWordIds == null || request.SessionWordIds.Count == 0 || request.SessionWordIds.Contains(x.Id))).Select(x => x.MotivationWordId).ToListAsync(cancellationToken);
        if (!source.HasWordHistory)
            ids.AddRange(await db.SessionWords.Where(x => x.SessionId == source.Id && (request.SessionWordIds == null || request.SessionWordIds.Count == 0 || request.SessionWordIds.Contains(x.Id))).Select(x => x.MotivationWordId).ToListAsync(cancellationToken));
        return await AddWordsAsync(new(target.Id, ids.Distinct().ToList()), cancellationToken);
    }
}
