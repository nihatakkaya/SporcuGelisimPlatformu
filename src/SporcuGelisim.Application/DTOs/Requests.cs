using SporcuGelisim.Domain.Enums;

namespace SporcuGelisim.Application.DTOs;

public sealed record RegisterAthleteRequest(string Email, string Password, string FirstName, string LastName);
public sealed record UpdateAthleteProfileRequest(
    Guid AthleteProfileId,
    Guid? PrimaryBranchId,
    string? NationalIdentityNumber,
    string? PhoneNumber,
    string? SecondaryPhoneNumber,
    string? ParentPhoneNumber,
    string? Address,
    string? Biography,
    DateOnly? BirthDate);
public sealed record CreateBranchRequest(string Name, string? Description, Guid? ParentBranchId, int DisplayOrder);
public sealed record UpdateBranchRequest(Guid Id, string Name, string? Description, Guid? ParentBranchId, int DisplayOrder, bool IsActive);
public sealed record CreateMotivationWordRequest(string Text, string? Description, Guid? ParentWordId, bool IsGlobal, IReadOnlyCollection<Guid> BranchIds);
public sealed record UpdateMotivationWordRequest(Guid Id, string Text, string? Description, Guid? ParentWordId, bool IsGlobal, bool IsActive, IReadOnlyCollection<Guid> BranchIds);
public sealed record AssignWordToBranchesRequest(Guid MotivationWordId, IReadOnlyCollection<Guid> BranchIds);
public sealed record AssignWordToAthletesRequest(Guid MotivationWordId, IReadOnlyCollection<Guid> AthleteProfileIds);
public sealed record CreateSessionRequest(Guid AthleteProfileId, string Title, DateTimeOffset SessionDate, string? Description);
public sealed record UpdateSessionRequest(Guid Id, string Title, DateTimeOffset SessionDate, string? Description, SessionStatus Status);
public sealed record AddSessionWordsRequest(Guid SessionId, IReadOnlyCollection<Guid> MotivationWordIds);
public sealed record CopySessionWordsRequest(Guid SourceSessionId, Guid TargetSessionId, IReadOnlyCollection<Guid>? SessionWordIds);
public sealed record ChangeAthleteWordsRequest(Guid AthleteProfileId, IReadOnlyCollection<Guid> AddWordIds, IReadOnlyCollection<Guid> RemoveWordIds, Guid? SessionId = null);
public sealed record SaveSessionNotesRequest(Guid SessionId, DateTimeOffset SessionDate, string? PrivateCoachNote, string? SharedNote);
public sealed record CreateFeedbackRequest(Guid AthleteProfileId, Guid? SessionId, Guid? RecipientUserId, DateTimeOffset FeedbackDate, string Comment);
public sealed record UpdateFeedbackRequest(Guid Id, DateTimeOffset FeedbackDate, string Comment);
public sealed record AssignAthleteRelationRequest(Guid AthleteProfileId, Guid RelatedUserId, AthleteRelationType RelationType);
public sealed record SubmitWordRequest(Guid AthleteProfileId, Guid TargetCoachUserId, string Text, string? Note);
public sealed record ReviewWordRequest(Guid Id, bool Approved, string? ReviewNote);
public sealed record UploadProfilePhotoRequest(Guid OwnerUserId, string FileName, string ContentType, long FileSize, Stream Content);
public sealed record SessionWordEdit(Guid MotivationWordId, bool Added);
public sealed record EditSessionRequest(Guid AthleteProfileId, Guid SessionId, string Revision, DateTimeOffset SessionDate, string? PrivateCoachNote, string? SharedNote, IReadOnlyCollection<Guid> RemovedChangeIds, IReadOnlyCollection<SessionWordEdit> NewChanges);
public sealed record DeleteSessionRequest(Guid AthleteProfileId, Guid SessionId, string Revision);
