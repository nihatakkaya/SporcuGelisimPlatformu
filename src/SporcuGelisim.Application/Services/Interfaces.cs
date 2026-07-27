using SporcuGelisim.Application.Common;
using SporcuGelisim.Application.DTOs;

namespace SporcuGelisim.Application.Services;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlySet<string> Roles { get; }
}

public interface IUserManagementService
{
    Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken);
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);
}

public interface IAthleteProfileService
{
    Task<AthleteProfileDto> GetAsync(Guid athleteProfileId, CancellationToken cancellationToken);
    Task<AthleteProfileDto> UpdateAsync(UpdateAthleteProfileRequest request, CancellationToken cancellationToken);
}

public interface IAthleteAccessService
{
    Task<bool> CanAccessAthleteAsync(Guid athleteProfileId, CancellationToken cancellationToken);
    Task EnsureCanAccessAthleteAsync(Guid athleteProfileId, CancellationToken cancellationToken);
    Task EnsureCanManageSessionAsync(Guid athleteProfileId, CancellationToken cancellationToken);
    Task EnsureCanCreateFeedbackAsync(Guid athleteProfileId, CancellationToken cancellationToken);
    Task EnsureCanManageWordAsync(Guid wordId, CancellationToken cancellationToken);
}

public interface IAthleteRelationService
{
    Task<AthleteRelationDto> AssignAsync(AssignAthleteRelationRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AthleteProfileDto>> GetRelatedAthletesAsync(CancellationToken cancellationToken);
}

public interface IBranchService
{
    Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken);
    Task<BranchDto> UpdateAsync(UpdateBranchRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<BranchDto>> GetTreeAsync(CancellationToken cancellationToken);
}

public interface IMotivationWordService
{
    Task<MotivationWordDto> CreateAsync(CreateMotivationWordRequest request, CancellationToken cancellationToken);
    Task<MotivationWordDto> UpdateAsync(UpdateMotivationWordRequest request, CancellationToken cancellationToken);
    Task AssignBranchesAsync(AssignWordToBranchesRequest request, CancellationToken cancellationToken);
    Task AssignAthletesAsync(AssignWordToAthletesRequest request, CancellationToken cancellationToken);
    Task RemoveAthleteAssignmentAsync(Guid assignmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MotivationWordDto>> GetForBranchAsync(Guid? branchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WordAssignmentDto>> GetAthleteAssignmentsAsync(CancellationToken cancellationToken);
    Task<WordRequestDto> SubmitWordRequestAsync(SubmitWordRequest request, CancellationToken cancellationToken);
    Task<WordRequestDto> ReviewWordRequestAsync(ReviewWordRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<WordRequestDto>> GetWordRequestsAsync(CancellationToken cancellationToken);
}

public interface IAthleteSessionService
{
    Task<AthleteSessionDto> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken);
    Task<AthleteSessionDto> UpdateAsync(UpdateSessionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AthleteSessionDto>> GetForAthleteAsync(Guid athleteProfileId, CancellationToken cancellationToken);
}

public interface ISessionWordService
{
    Task<IReadOnlyList<SessionWordDto>> AddWordsAsync(AddSessionWordsRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionWordDto>> CopyWordsAsync(CopySessionWordsRequest request, CancellationToken cancellationToken);
}

public interface IFeedbackService
{
    Task<FeedbackDto> CreateAsync(CreateFeedbackRequest request, CancellationToken cancellationToken);
    Task<FeedbackDto> UpdateAsync(UpdateFeedbackRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<FeedbackDto>> GetVisibleForAthleteAsync(Guid athleteProfileId, CancellationToken cancellationToken);
}

public interface IFileStorageService
{
    Task<FileAssetDto> SaveProfilePhotoAsync(UploadProfilePhotoRequest request, CancellationToken cancellationToken);
}

public interface IAuditService
{
    Task WriteAsync(string action, string entityName, string entityId, object? oldValues, object? newValues, CancellationToken cancellationToken);
}

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReportRowDto>> GetAthletesByBranchAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ReportRowDto>> GetTopWordsAsync(CancellationToken cancellationToken);
    Task<string> ExportWordUsageCsvAsync(CancellationToken cancellationToken);
    Task<string> ExportSessionSummaryCsvAsync(CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task<NotificationSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}
