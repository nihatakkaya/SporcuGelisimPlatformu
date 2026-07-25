namespace SporcuGelisim.Domain.Enums;

public enum RegistrationStatus
{
    Draft = 0,
    Active = 1,
    Suspended = 2
}

public enum AthleteRelationType
{
    Coach = 1,
    Parent = 2
}

public enum SessionStatus
{
    Draft = 0,
    Active = 1,
    Completed = 2,
    Cancelled = 3
}

public enum SelectionSource
{
    ManuallySelected = 0,
    CopiedFromPreviousSession = 1
}

public enum WordRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
