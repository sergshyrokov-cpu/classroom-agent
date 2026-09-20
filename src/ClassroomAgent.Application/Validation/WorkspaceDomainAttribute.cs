using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Validation;

/// <summary>
/// The shape of a Workspace domain (US-009 spec VR-002): 3 to 253 characters, at least two labels of 1 to 63
/// characters each, letters, digits and hyphens only, no hyphen at a label edge, one optional trailing dot.
/// </summary>
/// <remarks>
/// ASCII only: an internationalised domain is handled in its punycode form, which is what the Control Plane
/// stores and what Google reports (spec I-4).
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed partial class WorkspaceDomainAttribute : ValidationAttribute
{
    public const int MaximumLength = 253;

    public const int MinimumLength = 3;

    public override bool IsValid(object? value) =>
        value is string text && IsDomain(WorkspaceConnection.NormalizeDomain(text));

    /// <summary>Whether an already normalised domain satisfies VR-002.</summary>
    public static bool IsDomain(string domain) =>
        domain.Length is >= MinimumLength and <= MaximumLength && Shape().IsMatch(domain);

    [GeneratedRegex(@"^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$")]
    private static partial Regex Shape();
}
