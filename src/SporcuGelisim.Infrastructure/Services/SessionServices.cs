using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Domain.Enums;
using SporcuGelisim.Infrastructure.Data;

namespace SporcuGelisim.Infrastructure.Services;

public sealed class AthleteSessionService(ApplicationDbContext db, ICurrentUserService currentUser, IAthleteAccessService access, IAuditService audit)
    : IAthleteSessionService
{
    public async Task<AthleteSessionDto> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken)
    {
        await access.EnsureCanManageSessionAsync(request.AthleteProfileId, cancellationToken);
        var nextNumber = await db.AthleteSessions
            .Where(x => x.AthleteProfileId == request.AthleteProfileId)
            .Select(x => (int?)x.SessionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var entity = new AthleteSession
        {
            AthleteProfileId = request.AthleteProfileId,
            Title = request.Title.Trim(),
            SessionDate = request.SessionDate.ToUniversalTime(),
            Description = request.Description,
            SessionNumber = nextNumber + 1,
            CreatedByUserId = currentUser.UserId,
            Status = SessionStatus.Active
        };

        db.AthleteSessions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SessionCreated", nameof(AthleteSession), entity.Id.ToString(), null, entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<AthleteSessionDto> UpdateAsync(UpdateSessionRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.AthleteSessions.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Oturum bulunamadı.");
        await access.EnsureCanManageSessionAsync(entity.AthleteProfileId, cancellationToken);

        entity.Title = request.Title.Trim();
        entity.SessionDate = request.SessionDate.ToUniversalTime();
        entity.Description = request.Description;
        entity.Status = request.Status;
        entity.CompletedAt = request.Status == SessionStatus.Completed ? DateTimeOffset.UtcNow : entity.CompletedAt;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SessionUpdated", nameof(AthleteSession), entity.Id.ToString(), null, entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<AthleteSessionDto>> GetForAthleteAsync(Guid athleteProfileId, CancellationToken cancellationToken)
    {
        await access.EnsureCanAccessAthleteAsync(athleteProfileId, cancellationToken);
        return await db.AthleteSessions.AsNoTracking()
            .Where(x => x.AthleteProfileId == athleteProfileId)
            .OrderByDescending(x => x.SessionNumber)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
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

        var activeWords = await db.MotivationWords.AsNoTracking()
            .Where(x => request.MotivationWordIds.Contains(x.Id) && x.IsActive)
            .ToListAsync(cancellationToken);

        var existingIds = await db.SessionWords.AsNoTracking()
            .Where(x => x.SessionId == request.SessionId)
            .Select(x => x.MotivationWordId)
            .ToListAsync(cancellationToken);

        var displayOrder = await db.SessionWords
            .Where(x => x.SessionId == request.SessionId)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var newRows = activeWords
            .Where(x => !existingIds.Contains(x.Id))
            .Select(word => new SessionWord
            {
                SessionId = request.SessionId,
                MotivationWordId = word.Id,
                SelectedByUserId = currentUser.UserId,
                SelectedByRole = currentUser.Roles.FirstOrDefault() ?? string.Empty,
                SelectionSource = SelectionSource.ManuallySelected,
                WordTextSnapshot = word.Text,
                DisplayOrder = ++displayOrder
            })
            .ToList();

        db.SessionWords.AddRange(newRows);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SessionWordsAdded", nameof(SessionWord), request.SessionId.ToString(), null, newRows.Select(x => x.MotivationWordId), cancellationToken);
        return newRows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<SessionWordDto>> CopyWordsAsync(CopySessionWordsRequest request, CancellationToken cancellationToken)
    {
        if (request.SourceSessionId == request.TargetSessionId)
        {
            throw new ConflictException("Kaynak ve hedef oturum aynı olamaz.");
        }

        var sessions = await db.AthleteSessions.AsNoTracking()
            .Where(x => x.Id == request.SourceSessionId || x.Id == request.TargetSessionId)
            .ToListAsync(cancellationToken);
        var source = sessions.FirstOrDefault(x => x.Id == request.SourceSessionId) ?? throw new NotFoundException("Kaynak oturum bulunamadı.");
        var target = sessions.FirstOrDefault(x => x.Id == request.TargetSessionId) ?? throw new NotFoundException("Hedef oturum bulunamadı.");
        if (source.AthleteProfileId != target.AthleteProfileId)
        {
            throw new ForbiddenException("Başka sporcunun oturumundan kelime kopyalanamaz.");
        }

        await access.EnsureCanManageSessionAsync(target.AthleteProfileId, cancellationToken);

        var sourceWordsQuery = db.SessionWords.AsNoTracking().Where(x => x.SessionId == request.SourceSessionId);
        if (request.SessionWordIds is { Count: > 0 })
        {
            sourceWordsQuery = sourceWordsQuery.Where(x => request.SessionWordIds.Contains(x.Id));
        }

        var sourceWords = await sourceWordsQuery.ToListAsync(cancellationToken);
        var existingWordIds = await db.SessionWords.AsNoTracking()
            .Where(x => x.SessionId == request.TargetSessionId)
            .Select(x => x.MotivationWordId)
            .ToListAsync(cancellationToken);
        var activeWordIds = await db.MotivationWords.AsNoTracking()
            .Where(x => x.IsActive && sourceWords.Select(sw => sw.MotivationWordId).Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var displayOrder = await db.SessionWords
            .Where(x => x.SessionId == request.TargetSessionId)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var copied = sourceWords
            .Where(x => activeWordIds.Contains(x.MotivationWordId) && !existingWordIds.Contains(x.MotivationWordId))
            .Select(x => new SessionWord
            {
                SessionId = request.TargetSessionId,
                MotivationWordId = x.MotivationWordId,
                SelectedByUserId = currentUser.UserId,
                SelectedByRole = currentUser.Roles.FirstOrDefault() ?? string.Empty,
                SelectionSource = SelectionSource.CopiedFromPreviousSession,
                CopiedFromSessionWordId = x.Id,
                WordTextSnapshot = x.WordTextSnapshot,
                DisplayOrder = ++displayOrder
            })
            .ToList();

        db.SessionWords.AddRange(copied);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("SessionWordsCopied", nameof(SessionWord), request.TargetSessionId.ToString(), null, copied.Select(x => x.Id), cancellationToken);
        return copied.Select(ToDto).ToList();
    }

    private static SessionWordDto ToDto(SessionWord entity) =>
        new(entity.Id, entity.SessionId, entity.MotivationWordId, entity.WordTextSnapshot, entity.SelectionSource);
}
