using SporcuGelisim.Domain.Enums;

namespace SporcuGelisim.Application.DTOs;

public sealed record BranchDto(Guid Id, string Name, string Slug, string? Description, Guid? ParentBranchId, bool IsActive, int DisplayOrder);
public sealed record MotivationWordDto(Guid Id, string Text, string? Description, Guid? ParentWordId, bool IsGlobal, bool IsActive, Guid? CreatedByUserId);
public sealed record AthleteProfileDto(
    Guid Id,
    Guid UserId,
    string? FullName,
    Guid? PrimaryBranchId,
    string? BranchName,
    string? NationalIdentityNumber,
    string? PhoneNumber,
    string? SecondaryPhoneNumber,
    string? ParentPhoneNumber,
    string? Address,
    string? Biography,
    DateOnly? BirthDate,
    string? ProfilePhotoPath);
public sealed record AthleteRelationDto(Guid Id, Guid AthleteProfileId, Guid RelatedUserId, AthleteRelationType RelationType, bool IsActive);
public sealed record AthleteSessionDto(Guid Id, Guid AthleteProfileId, string Title, int SessionNumber, DateTimeOffset SessionDate, SessionStatus Status);
public sealed record SessionWordDto(Guid Id, Guid SessionId, Guid MotivationWordId, string WordTextSnapshot, SelectionSource SelectionSource);
public sealed record WordAssignmentDto(Guid Id, Guid MotivationWordId, string WordText, Guid AthleteProfileId, string AthleteName, bool IsActive);
public sealed record WordRequestDto(
    Guid Id,
    Guid AthleteProfileId,
    string AthleteName,
    Guid TargetCoachUserId,
    string CoachName,
    string Text,
    string? Note,
    WordRequestStatus Status,
    string? ReviewNote,
    DateTimeOffset CreatedAt);
public sealed record FeedbackDto(Guid Id, Guid AthleteProfileId, Guid? SessionId, Guid AuthorUserId, Guid? RecipientUserId, DateTimeOffset FeedbackDate, DateTimeOffset? RecipientViewedAt, string Comment);
public sealed record FileAssetDto(Guid Id, string OriginalFileName, string ContentType, long FileSize, string RelativePath);
public sealed record DashboardSummaryDto(int AthleteCount, int CoachCount, int ParentCount, int SessionCount);
public sealed record NotificationSummaryDto(int UnreadFeedbackCount, int PendingWordRequestCount)
{
    public int TotalCount => UnreadFeedbackCount + PendingWordRequestCount;
}
public sealed record ReportRowDto(string Label, int Count);
