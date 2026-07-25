using SporcuGelisim.Domain.Enums;

namespace SporcuGelisim.Application.DTOs;

public sealed record RegisterAthleteRequest(string Email, string Password, string FirstName, string LastName);
public sealed record UpdateAthleteProfileRequest(Guid AthleteProfileId, Guid? PrimaryBranchId, string? Biography, DateOnly? BirthDate);
public sealed record CreateBranchRequest(string Name, string? Description, Guid? ParentBranchId, int DisplayOrder);
public sealed record UpdateBranchRequest(Guid Id, string Name, string? Description, Guid? ParentBranchId, int DisplayOrder, bool IsActive);
public sealed record CreateMotivationWordRequest(string Text, string? Description, Guid? ParentWordId, bool IsGlobal, IReadOnlyCollection<Guid> BranchIds);
public sealed record UpdateMotivationWordRequest(Guid Id, string Text, string? Description, Guid? ParentWordId, bool IsGlobal, bool IsActive, IReadOnlyCollection<Guid> BranchIds);
public sealed record AssignWordToBranchesRequest(Guid MotivationWordId, IReadOnlyCollection<Guid> BranchIds);
public sealed record CreateSessionRequest(Guid AthleteProfileId, string Title, DateTimeOffset SessionDate, string? Description);
public sealed record UpdateSessionRequest(Guid Id, string Title, DateTimeOffset SessionDate, string? Description, SessionStatus Status);
public sealed record AddSessionWordsRequest(Guid SessionId, IReadOnlyCollection<Guid> MotivationWordIds);
public sealed record CopySessionWordsRequest(Guid SourceSessionId, Guid TargetSessionId, IReadOnlyCollection<Guid>? SessionWordIds);
public sealed record CreateFeedbackRequest(Guid AthleteProfileId, Guid? SessionId, DateTimeOffset FeedbackDate, string Comment);
public sealed record UpdateFeedbackRequest(Guid Id, DateTimeOffset FeedbackDate, string Comment);
public sealed record AssignAthleteRelationRequest(Guid AthleteProfileId, Guid RelatedUserId, AthleteRelationType RelationType);
public sealed record UploadProfilePhotoRequest(Guid OwnerUserId, string FileName, string ContentType, long FileSize, Stream Content);
