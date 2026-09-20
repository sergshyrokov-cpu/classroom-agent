using System.ComponentModel.DataAnnotations;
using ClassroomAgent.Domain.Entities;

namespace ClassroomAgent.Application.Validation;

/// <summary>
/// The shape of the school's technical account (US-009 spec VR-001): exactly one <c>@</c>, non-empty parts, no
/// whitespace, at most 254 characters, and a domain part that satisfies <see cref="WorkspaceDomainAttribute"/>.
/// </summary>
/// <remarks>
/// Deliberately the service-channel rule of US-008 VR-006 rather than the stricter <c>AllowedAdmin</c> rule of
/// US-003: the account is created by the school's super-admin and its name part is not ours to constrain
/// (spec I-3). The rejected value is never logged (SC-10).
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ServiceAccountEmailAttribute : ValidationAttribute
{
    public const int MaximumLength = 254;

    public override bool IsValid(object? value)
    {
        if (value is not string text)
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length is 0 or > MaximumLength)
        {
            return false;
        }

        if (trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var parts = trimmed.Split('@');
        return parts.Length == 2
            && parts[0].Length > 0
            && WorkspaceDomainAttribute.IsDomain(WorkspaceConnection.NormalizeDomain(parts[1]));
    }
}
