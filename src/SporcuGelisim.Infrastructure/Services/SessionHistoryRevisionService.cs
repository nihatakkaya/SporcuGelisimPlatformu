using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;

namespace SporcuGelisim.Infrastructure.Services;

public sealed partial class AthleteSessionService
{
    private static string RevisionOf(SessionDetailDto detail) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(detail with { Revision = "" }))));

    public Task EditAsync(EditSessionRequest request, CancellationToken cancellationToken) =>
        ReviseAsync(request.AthleteProfileId, request.SessionId, request.Revision, request, cancellationToken);

    public Task DeleteAsync(DeleteSessionRequest request, CancellationToken cancellationToken) =>
        ReviseAsync(request.AthleteProfileId, request.SessionId, request.Revision, null, cancellationToken);

    private async Task ReviseAsync(Guid athleteId, Guid sessionId, string revision, EditSessionRequest? edit, CancellationToken ct)
    {
        await access.EnsureCanManageSessionAsync(athleteId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var detail = await GetDetailAsync(athleteId, sessionId, ct);
            if (edit is null && !detail.CanDelete)
                throw new ForbiddenException("Yalnızca kendi oluşturduğunuz oturumları silebilirsiniz.");
            if (detail.Revision != revision)
                throw new ConflictException("Oturum başka bir işlemle güncellendi. Yenileyip değişikliklerinizi tekrar kontrol edin.");
            if (edit?.PrivateCoachNote?.Length > 10000 || edit?.SharedNote?.Length > 10000)
                throw new ConflictException("Notlar en fazla 10.000 karakter olabilir.");

            var affected = await db.AthleteSessions.Where(x => x.AthleteProfileId == athleteId && x.SessionNumber >= detail.Session.SessionNumber)
                .OrderBy(x => x.SessionNumber).ToListAsync(ct);
            foreach (var row in affected) await db.Entry(row).ReloadAsync(ct);
            var target = affected.Single(x => x.Id == sessionId);
            if (edit is not null && edit.RemovedChangeIds.Count == 0 && edit.NewChanges.Count == 0)
            {
                target.SessionDate = new DateTimeOffset(edit.SessionDate.Date, TimeSpan.FromHours(3));
                target.PrivateCoachNote = edit.PrivateCoachNote?.Trim();
                target.SharedNote = edit.SharedNote?.Trim();
                target.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                await audit.WriteAsync("SessionNotesCorrected", nameof(AthleteSession), sessionId.ToString(), null, new { AthleteProfileId = athleteId }, ct);
                await transaction.CommitAsync(ct);
                return;
            }            var sessionIds = affected.Select(x => x.Id).ToArray();
            var snapshots = await db.SessionWordSnapshots.Where(x => sessionIds.Contains(x.SessionId)).ToListAsync(ct);
            var state = detail.BeginningWords.ToDictionary(x => x.MotivationWordId, x => x.Text);
            if (!detail.HasWordHistory && !snapshots.Any(x => x.SessionId == sessionId))
            {
                var previousId = await db.AthleteSessions.Where(x => x.AthleteProfileId == athleteId && x.SessionNumber < target.SessionNumber)
                    .OrderByDescending(x => x.SessionNumber).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
                var previous = await db.AthleteSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == previousId, ct);
                var hasPreviousSnapshot = previous is not null && (previous.HasWordHistory || await db.SessionWordSnapshots.AnyAsync(x => x.SessionId == previous.Id, ct));
                if (hasPreviousSnapshot)
                    state = await db.SessionWordSnapshots.Where(x => x.SessionId == previousId && !x.IsBeginning).ToDictionaryAsync(x => x.MotivationWordId, x => x.WordText, ct);
                else
                {
                    // Preserve unrecorded pre-existing assignments when importing legacy selections.
                    // Prefer the next known starting state; otherwise work back from current assignments.
                    var nextKnown = affected.Skip(1).FirstOrDefault(x => x.HasWordHistory || snapshots.Any(s => s.SessionId == x.Id));
                    state = nextKnown is null
                        ? (await AthleteWordWorkflow.ReadCurrentAsync(db, athleteId, ct)).ToDictionary(x => x.MotivationWordId, x => x.Text)
                        : snapshots.Where(x => x.SessionId == nextKnown.Id && x.IsBeginning).ToDictionary(x => x.MotivationWordId, x => x.WordText);
                    var prefixIds = affected.Where(x => nextKnown is null || x.SessionNumber < nextKnown.SessionNumber).Select(x => x.Id).ToArray();
                    var knownEvents = await db.AthleteWordChanges.AsNoTracking().Where(x => x.AthleteProfileId == athleteId && x.SessionId.HasValue && prefixIds.Contains(x.SessionId.Value)).ToListAsync(ct);
                    foreach (var change in knownEvents.OrderByDescending(x => affected.First(a => a.Id == x.SessionId).SessionNumber).ThenByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id))
                    {
                        if (change.Added) state.Remove(change.MotivationWordId);
                        else state[change.MotivationWordId] = change.WordText;
                    }
                    var legacyWordIds = await db.SessionWords.Where(x => prefixIds.Contains(x.SessionId)).Select(x => x.MotivationWordId).ToListAsync(ct);
                    foreach (var id in legacyWordIds) state.Remove(id);
                }            }
            var events = await db.AthleteWordChanges.Where(x => x.AthleteProfileId == athleteId && x.SessionId.HasValue && sessionIds.Contains(x.SessionId.Value)).ToListAsync(ct);
            // Legacy selected words have no events. Preserve their known selections as explicit additions.
            foreach (var legacy in affected.Where(x => !x.HasWordHistory && !snapshots.Any(s => s.SessionId == x.Id)))
            {
                var selections = await db.SessionWords.Where(x => x.SessionId == legacy.Id).ToListAsync(ct);
                foreach (var selection in selections.Where(x => !events.Any(e => e.SessionId == legacy.Id && e.MotivationWordId == x.MotivationWordId)))
                {
                    var change = new AthleteWordChange { Id = selection.Id, AthleteProfileId = athleteId, SessionId = legacy.Id, MotivationWordId = selection.MotivationWordId,
                        WordText = selection.WordTextSnapshot, Added = true, Source = "session", ChangedByUserId = currentUser.UserId!.Value, CreatedAt = selection.CreatedAt };
                    db.AthleteWordChanges.Add(change);
                    events.Add(change);
                }
            }
            if (edit is null)
            {
                target.IsDeleted = true;
                foreach (var change in events.Where(x => x.SessionId == sessionId)) change.IsDeleted = true;
                foreach (var word in await db.SessionWords.Where(x => x.SessionId == sessionId).ToListAsync(ct)) word.IsDeleted = true;
            }
            else
            {
                var removed = edit.RemovedChangeIds.Distinct().ToHashSet();
                if (removed.Any(id => !events.Any(x => x.Id == id && x.SessionId == sessionId)))
                    throw new ConflictException("Kelime değişikliği bu oturuma ait değil veya artık mevcut değil.");
                foreach (var change in events.Where(x => removed.Contains(x.Id))) change.IsDeleted = true;
                var newChanges = edit.NewChanges.Distinct().ToList();
                if (newChanges.GroupBy(x => x.MotivationWordId).Any(x => x.Count() > 1))
                    throw new ConflictException("Aynı kelime için tek bir yeni işlem seçin.");
                var wordIds = newChanges.Select(x => x.MotivationWordId).ToArray();
                var available = await db.MotivationWords.AsNoTracking().Where(x => wordIds.Contains(x.Id) && x.IsActive &&
                    (x.IsGlobal || x.CreatedByUserId == currentUser.UserId || currentUser.Roles.Contains(RoleNames.Admin))).ToListAsync(ct);
                foreach (var item in newChanges)
                {
                    if (events.Any(x => x.SessionId == sessionId && !x.IsDeleted && x.MotivationWordId == item.MotivationWordId && x.Added == item.Added)) continue;
                    var text = available.FirstOrDefault(x => x.Id == item.MotivationWordId)?.Text;
                    // Existing historical words remain removable even if the pool word is now inactive.
                    if (!item.Added) text ??= detail.BeginningWords.Concat(detail.EndingWords).FirstOrDefault(x => x.MotivationWordId == item.MotivationWordId)?.Text;
                    if (text is null) throw new ForbiddenException("Seçilen kelime kullanılamıyor veya erişiminiz yok.");
                    var change = new AthleteWordChange { AthleteProfileId = athleteId, SessionId = sessionId, MotivationWordId = item.MotivationWordId,
                        WordText = text, Added = item.Added, Source = "session", ChangedByUserId = currentUser.UserId!.Value };
                    db.AthleteWordChanges.Add(change);
                    events.Add(change);
                }
                target.SessionDate = new DateTimeOffset(edit.SessionDate.Date, TimeSpan.FromHours(3));
                target.PrivateCoachNote = edit.PrivateCoachNote?.Trim();
                target.SharedNote = edit.SharedNote?.Trim();
            }
            target.UpdatedAt = DateTimeOffset.UtcNow;
            // Delete and rebuild only derived snapshots, inside the same transaction.
            db.SessionWordSnapshots.RemoveRange(snapshots);
            await db.SaveChangesAsync(ct);
            foreach (var session in affected.Where(x => !x.IsDeleted))
            {
                session.HasWordHistory = true;
                AddPhase(session.Id, true);
                foreach (var change in events.Where(x => x.SessionId == session.Id && !x.IsDeleted).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                {
                    if (change.Added) state[change.MotivationWordId] = change.WordText;
                    else state.Remove(change.MotivationWordId);
                }
                AddPhase(session.Id, false);
            }
            var assignments = await db.AthleteWordAssignments.Where(x => x.AthleteProfileId == athleteId && x.IsActive).ToListAsync(ct);
            foreach (var assignment in assignments.Where(x => !state.ContainsKey(x.MotivationWordId)))
            {
                assignment.IsActive = false;
                assignment.UpdatedAt = DateTimeOffset.UtcNow;
            }
            foreach (var wordId in state.Keys.Where(id => assignments.All(x => x.MotivationWordId != id)))
                db.AthleteWordAssignments.Add(new AthleteWordAssignment { AthleteProfileId = athleteId, MotivationWordId = wordId, AssignedByUserId = currentUser.UserId!.Value });
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(edit is null ? "SessionDeleted" : "SessionHistoryCorrected", nameof(AthleteSession), sessionId.ToString(), null,
                new { AthleteProfileId = athleteId, AffectedSessions = sessionIds }, ct);
            await transaction.CommitAsync(ct);

            void AddPhase(Guid id, bool beginning)
            {
                foreach (var word in state)
                    db.SessionWordSnapshots.Add(new SessionWordSnapshot { SessionId = id, MotivationWordId = word.Key, WordText = word.Value, IsBeginning = beginning });
            }
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
