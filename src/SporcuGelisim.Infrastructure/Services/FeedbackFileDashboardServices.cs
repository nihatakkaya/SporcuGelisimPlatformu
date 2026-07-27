using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;
using SporcuGelisim.Application.Services;
using SporcuGelisim.Domain.Common;
using SporcuGelisim.Domain.Entities;
using SporcuGelisim.Domain.Enums;
using SporcuGelisim.Infrastructure.Data;

namespace SporcuGelisim.Infrastructure.Services;

public sealed class FeedbackService(ApplicationDbContext db, ICurrentUserService currentUser, IAthleteAccessService access, IAuditService audit) : IFeedbackService
{
    public async Task<FeedbackDto> CreateAsync(CreateFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            throw new ForbiddenException("Geri bildirim için oturum açmanız gerekir.");
        }

        await access.EnsureCanCreateFeedbackAsync(request.AthleteProfileId, cancellationToken);
        if (currentUser.Roles.Contains(RoleNames.Athlete))
        {
            if (!request.RecipientUserId.HasValue)
            {
                throw new ValidationFailedException(new Dictionary<string, string[]> { ["RecipientUserId"] = ["Geri bildirimin gönderileceği antrenör seçilmelidir."] });
            }

            var candidateRelations = await db.AthleteRelations.AsNoTracking()
                .Where(x =>
                x.AthleteProfileId == request.AthleteProfileId &&
                x.RelatedUserId == request.RecipientUserId.Value &&
                x.RelationType == AthleteRelationType.Coach &&
                x.IsActive)
                .Select(x => x.EndDate)
                .ToListAsync(cancellationToken);
            var isResponsibleCoach = candidateRelations.Any(endDate => endDate is null || endDate > DateTimeOffset.UtcNow);
            if (!isResponsibleCoach)
            {
                throw new ForbiddenException("Yalnızca sorumlu antrenörünüze geri bildirim gönderebilirsiniz.");
            }
        }

        if (request.SessionId.HasValue)
        {
            var matchesAthlete = await db.AthleteSessions.AnyAsync(x => x.Id == request.SessionId && x.AthleteProfileId == request.AthleteProfileId, cancellationToken);
            if (!matchesAthlete)
            {
                throw new ForbiddenException("Oturum bu sporcuya ait değil.");
            }
        }

        var entity = new Feedback
        {
            AthleteProfileId = request.AthleteProfileId,
            SessionId = request.SessionId,
            AuthorUserId = currentUser.UserId.Value,
            RecipientUserId = request.RecipientUserId,
            FeedbackDate = request.FeedbackDate.ToUniversalTime(),
            Comment = request.Comment.Trim()
        };
        db.Feedbacks.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("FeedbackCreated", nameof(Feedback), entity.Id.ToString(), null, entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<FeedbackDto> UpdateAsync(UpdateFeedbackRequest request, CancellationToken cancellationToken)
    {
        var entity = await db.Feedbacks.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Geri bildirim bulunamadı.");
        if (!currentUser.Roles.Contains(RoleNames.Admin) && entity.AuthorUserId != currentUser.UserId)
        {
            throw new ForbiddenException("Yalnızca kendi geri bildiriminizi düzenleyebilirsiniz.");
        }

        entity.FeedbackDate = request.FeedbackDate.ToUniversalTime();
        entity.Comment = request.Comment.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("FeedbackUpdated", nameof(Feedback), entity.Id.ToString(), null, entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<FeedbackDto>> GetVisibleForAthleteAsync(Guid athleteProfileId, CancellationToken cancellationToken)
    {
        await access.EnsureCanAccessAthleteAsync(athleteProfileId, cancellationToken);
        var query = db.Feedbacks.AsNoTracking().Where(x => x.AthleteProfileId == athleteProfileId);
        if (!currentUser.Roles.Contains(RoleNames.Admin))
        {
            query = query.Where(x => x.AuthorUserId == currentUser.UserId || x.RecipientUserId == currentUser.UserId);
        }

        return await query.OrderByDescending(x => x.FeedbackDate).Select(x => ToDto(x)).ToListAsync(cancellationToken);
    }

    private static FeedbackDto ToDto(Feedback entity) =>
        new(entity.Id, entity.AthleteProfileId, entity.SessionId, entity.AuthorUserId, entity.RecipientUserId, entity.FeedbackDate, entity.RecipientViewedAt, entity.Comment);
}

public sealed class FileStorageService(ApplicationDbContext db, IWebHostEnvironment environment) : IFileStorageService
{
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [[0xFF, 0xD8, 0xFF]],
        ["image/png"] = [[0x89, 0x50, 0x4E, 0x47]],
        ["image/webp"] = [[0x52, 0x49, 0x46, 0x46]]
    };

    public async Task<FileAssetDto> SaveProfilePhotoAsync(UploadProfilePhotoRequest request, CancellationToken cancellationToken)
    {
        if (request.FileSize <= 0 || request.FileSize > 5 * 1024 * 1024)
        {
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["File"] = ["Fotoğraf en fazla 5 MB olabilir."] });
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(extension) || !Signatures.ContainsKey(request.ContentType))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["File"] = ["Sadece jpg, jpeg, png veya webp yüklenebilir."] });
        }

        await using var buffer = new MemoryStream();
        await request.Content.CopyToAsync(buffer, cancellationToken);
        var header = buffer.ToArray().Take(12).ToArray();
        if (!Signatures[request.ContentType].Any(sig => header.Length >= sig.Length && sig.SequenceEqual(header.Take(sig.Length))))
        {
            throw new ValidationFailedException(new Dictionary<string, string[]> { ["File"] = ["Dosya imzası geçersiz."] });
        }

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var uploadRoot = Path.Combine(environment.ContentRootPath, "uploads", "profiles");
        Directory.CreateDirectory(uploadRoot);
        var fullPath = Path.Combine(uploadRoot, storedName);
        buffer.Position = 0;
        await using (var output = File.Create(fullPath))
        {
            await buffer.CopyToAsync(output, cancellationToken);
        }

        var asset = new FileAsset
        {
            OwnerUserId = request.OwnerUserId,
            OriginalFileName = Path.GetFileName(request.FileName),
            StoredFileName = storedName,
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            RelativePath = $"uploads/profiles/{storedName}"
        };
        db.FileAssets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        return new FileAssetDto(asset.Id, asset.OriginalFileName, asset.ContentType, asset.FileSize, asset.RelativePath);
    }
}

public sealed class AuditService(ApplicationDbContext db, ICurrentUserService currentUser) : IAuditService
{
    public async Task WriteAsync(string action, string entityName, string entityId, object? oldValues, object? newValues, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues)
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DashboardService(ApplicationDbContext db) : IDashboardService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var athleteCount = await db.AthleteProfiles.CountAsync(cancellationToken);
        var coachCount = await CountRoleAsync(RoleNames.Coach, cancellationToken);
        var parentCount = await CountRoleAsync(RoleNames.Parent, cancellationToken);
        var sessionCount = await db.AthleteSessions.CountAsync(cancellationToken);
        return new DashboardSummaryDto(athleteCount, coachCount, parentCount, sessionCount);
    }

    public async Task<IReadOnlyList<ReportRowDto>> GetAthletesByBranchAsync(CancellationToken cancellationToken) =>
        await (from profile in db.AthleteProfiles.AsNoTracking()
               join branch in db.SportBranches.AsNoTracking() on profile.PrimaryBranchId equals branch.Id into branchJoin
               from branch in branchJoin.DefaultIfEmpty()
               group profile by branch == null ? "Branşsız" : branch.Name into g
               select new ReportRowDto(g.Key, g.Count())).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReportRowDto>> GetTopWordsAsync(CancellationToken cancellationToken) =>
        await (from sessionWord in db.SessionWords.AsNoTracking()
               group sessionWord by sessionWord.WordTextSnapshot into g
               orderby g.Count() descending
               select new ReportRowDto(g.Key, g.Count())).Take(20).ToListAsync(cancellationToken);

    public async Task<string> ExportWordUsageCsvAsync(CancellationToken cancellationToken) =>
        ToCsv(await GetTopWordsAsync(cancellationToken));

    public async Task<string> ExportSessionSummaryCsvAsync(CancellationToken cancellationToken)
    {
        var rows = await db.AthleteSessions.AsNoTracking()
            .GroupBy(x => x.AthleteProfileId)
            .Select(x => new ReportRowDto(x.Key.ToString(), x.Count()))
            .ToListAsync(cancellationToken);
        return ToCsv(rows);
    }

    private async Task<int> CountRoleAsync(string roleName, CancellationToken cancellationToken) =>
        await (from userRole in db.UserRoles.AsNoTracking()
               join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
               where role.Name == roleName
               select userRole.UserId).Distinct().CountAsync(cancellationToken);

    private static string ToCsv(IEnumerable<ReportRowDto> rows)
    {
        var builder = new StringBuilder("\uFEFFEtiket,Adet\r\n");
        foreach (var row in rows)
        {
            builder.Append('"').Append(row.Label.Replace("\"", "\"\"")).Append("\",").Append(row.Count).Append("\r\n");
        }

        return builder.ToString();
    }
}

public sealed class NotificationService(IDbContextFactory<ApplicationDbContext> dbContextFactory, ICurrentUserService currentUser) : INotificationService
{
    public async Task<NotificationSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        if (currentUser.UserId is null)
        {
            return new NotificationSummaryDto(0, 0);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var userId = currentUser.UserId.Value;
        var unreadFeedbackCount = await db.Feedbacks.AsNoTracking()
            .CountAsync(x => x.RecipientUserId == userId && x.RecipientViewedAt == null, cancellationToken);

        var pendingWordRequestQuery = db.AthleteWordRequests.AsNoTracking()
            .Where(x => x.Status == SporcuGelisim.Domain.Enums.WordRequestStatus.Pending);
        if (!currentUser.Roles.Contains(RoleNames.Admin))
        {
            pendingWordRequestQuery = currentUser.Roles.Contains(RoleNames.Coach)
                ? pendingWordRequestQuery.Where(x => x.TargetCoachUserId == userId)
                : pendingWordRequestQuery.Where(x => false);
        }

        var pendingWordRequestCount = await pendingWordRequestQuery.CountAsync(cancellationToken);
        return new NotificationSummaryDto(unreadFeedbackCount, pendingWordRequestCount);
    }
}
