using System.ComponentModel.DataAnnotations;
using ClassroomAgent.Application.Validation;

namespace ClassroomAgent.Application.Models.Requests;

/// <summary>
/// The whole save request (US-009 openapi <c>SaveWorkspaceConnectionForm</c>; spec VR-003). It has exactly one
/// field: per OD-001 the domain is displayed, not typed, so a domain arriving under any name has no property
/// to bind to. The domain written comes from <c>LegitimacyState</c> at the moment of the save.
/// </summary>
public sealed class SaveWorkspaceConnectionRequest
{
    /// <summary>The school's technical account — not the Admin's own account and not a super-admin (BR-015).</summary>
    [Required(ErrorMessage = "WorkspaceConnection.Validation.Required")]
    [StringLength(ServiceAccountEmailAttribute.MaximumLength, ErrorMessage = "WorkspaceConnection.Validation.TooLong")]
    [ServiceAccountEmail(ErrorMessage = "WorkspaceConnection.Validation.Email")]
    public string ImpersonationUserEmail { get; init; } = string.Empty;
}
