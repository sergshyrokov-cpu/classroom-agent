namespace ClassroomAgent.Application.Authorization;

/// <summary>
/// The cells of the <c>trebovaniya.md</c> §2 permission matrix this Story needs, as policy names
/// (US-008 spec FR-004; SC-4). The matrix lives in one place so it can be compared against the requirements;
/// the rest of it is not implemented speculatively.
/// </summary>
public static class InstallationPolicies
{
    /// <summary>"Просмотр статуса легитимности" — visible to Admin and Dean alike.</summary>
    public const string ViewLegitimacyStatus = "ViewLegitimacyStatus";

    /// <summary>Any authenticated account of the installation; sign-out needs nothing more.</summary>
    public const string AuthenticatedUser = "AuthenticatedUser";
}
