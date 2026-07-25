namespace SporcuGelisim.Domain.Common;

public static class AuthorizationPolicyNames
{
    public const string AdminOnly = "AdminOnly";
    public const string AthleteOnly = "AthleteOnly";
    public const string CoachOnly = "CoachOnly";
    public const string ParentOnly = "ParentOnly";
    public const string AthleteOrAdmin = "AthleteOrAdmin";
    public const string CoachOrAdmin = "CoachOrAdmin";
    public const string CanAccessAthlete = "CanAccessAthlete";
    public const string CanManageSession = "CanManageSession";
    public const string CanCreateFeedback = "CanCreateFeedback";
    public const string CanManageWord = "CanManageWord";
}
