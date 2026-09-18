using System.Data;
using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Infrastructure.Data;

namespace SporcuGelisim.Infrastructure.Services;

public sealed class AthleteWordWorkflow(ApplicationDbContext db, ICurrentUserService currentUser, IAthleteAccessService access) : IAthleteWordWorkflow
{
    public async Task<IReadOnlyList<WordSnapshotDto>> GetCurrentAsync(Guid athleteProfileId, CancellationToken cancellationToken)
    {
        await access.EnsureCanAccessAthleteAsync(athleteProfileId, cancellationToken);
        return await ReadCurrentAsync(db, athleteProfileId, cancellationToken);
    }

    internal static async Task<List<WordSnapshotDto>> ReadCurrentAsync(ApplicationDbContext db, Guid athleteId, CancellationToken ct) =>
        await (from assignment in db.AthleteWordAssignments.AsNoTracking()
               join word in db.MotivationWords.IgnoreQueryFilters().AsNoTracking() on assignment.MotivationWordId equals word.Id
               where assignment.AthleteProfileId == athleteId && assignment.IsActive
               orderby word.Text
               select new WordSnapshotDto(word.Id, word.Text)).ToListAsync(ct);

    internal static void AddSnapshots(ApplicationDbContext db, Guid sessionId, IEnumerable<WordSnapshotDto> words)
    {
        foreach (var word in words)
            foreach (var beginning in new[] { true, false })
                db.SessionWordSnapshots.Add(new SessionWordSnapshot { SessionId = sessionId, MotivationWordId = word.MotivationWordId, WordText = word.Text, IsBeginning = beginning });
    }

    public async Task ChangeAsync(ChangeAthleteWordsRequest request, CancellationToken cancellationToken)
    {
        await access.EnsureCanManageSessionAsync(request.AthleteProfileId, cancellationToken);
        if (request.AddWordIds.Intersect(request.RemoveWordIds).Any())
            throw new ConflictException("Bir kelime aynı işlemde hem eklenemez hem çıkarılamaz.");
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        try
        {
            var latest = await db.AthleteSessions.Where(x => x.AthleteProfileId == request.AthleteProfileId)
                .OrderByDescending(x => x.SessionNumber).FirstOrDefaultAsync(cancellationToken);
            if (request.SessionId.HasValue && latest?.Id != request.SessionId)
                throw new ConflictException("Geçmişi korumak için kelimeler yalnızca en son oturumda değiştirilebilir.");
            var ids = request.AddWordIds.Distinct().ToArray();
            var words = await db.MotivationWords.AsNoTracking().Where(x => ids.Contains(x.Id) && x.IsActive &&
                (x.IsGlobal || x.CreatedByUserId == currentUser.UserId || currentUser.Roles.Contains(RoleNames.Admin))).ToListAsync(cancellationToken);
            if (words.Count != ids.Length)
                throw new ForbiddenException("Seçilen kelimelerden biri kullanılamıyor veya erişiminiz yok.");
            var assignments = await db.AthleteWordAssignments.Where(x => x.AthleteProfileId == request.AthleteProfileId && x.IsActive).ToListAsync(cancellationToken);
            var current = await ReadCurrentAsync(db, request.AthleteProfileId, cancellationToken);
            // Legacy sessions cannot reconstruct an unknown starting state. Keep HasWordHistory false.
            if (latest is not null && !latest.HasWordHistory && !await db.SessionWordSnapshots.AnyAsync(x => x.SessionId == latest.Id, cancellationToken))
                AddSnapshots(db, latest.Id, current);
            var endings = latest is null ? [] : await db.SessionWordSnapshots.Where(x => x.SessionId == latest.Id && !x.IsBeginning).ToListAsync(cancellationToken);
            endings.AddRange(db.SessionWordSnapshots.Local.Where(x => latest != null && x.SessionId == latest.Id && !x.IsBeginning && !endings.Contains(x)).ToList());
            foreach (var assignment in assignments.Where(x => request.RemoveWordIds.Contains(x.MotivationWordId)))
            {
                assignment.IsActive = false;
                assignment.UpdatedAt = DateTimeOffset.UtcNow;
                var text = current.First(x => x.MotivationWordId == assignment.MotivationWordId).Text;
                Record(assignment.MotivationWordId, text, false);
                var snapshot = endings.FirstOrDefault(x => x.MotivationWordId == assignment.MotivationWordId);
                if (snapshot is not null) db.SessionWordSnapshots.Remove(snapshot);
            }
            foreach (var word in words.Where(x => assignments.All(a => a.MotivationWordId != x.Id)))
            {
                db.AthleteWordAssignments.Add(new AthleteWordAssignment { AthleteProfileId = request.AthleteProfileId, MotivationWordId = word.Id, AssignedByUserId = currentUser.UserId!.Value });
                Record(word.Id, word.Text, true);
                if (latest is not null)
                    db.SessionWordSnapshots.Add(new SessionWordSnapshot { SessionId = latest.Id, MotivationWordId = word.Id, WordText = word.Text });
            }
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            void Record(Guid wordId, string text, bool added) => db.AthleteWordChanges.Add(new AthleteWordChange
            {
                AthleteProfileId = request.AthleteProfileId, SessionId = latest?.Id, MotivationWordId = wordId,
                WordText = text, Added = added, Source = request.SessionId.HasValue ? "session" : "assigned_words", ChangedByUserId = currentUser.UserId!.Value
            });
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}

