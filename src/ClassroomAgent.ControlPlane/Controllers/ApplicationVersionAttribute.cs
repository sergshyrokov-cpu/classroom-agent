using System.ComponentModel.DataAnnotations;
using ClassroomAgent.ControlPlane.Services;

namespace ClassroomAgent.ControlPlane.Controllers;

/// <summary>
/// VR-002 for an installation application version: <c>MAJOR.MINOR.PATCH</c>, each part 0–999999 without
/// leading zeros, at most 20 characters. A missing value is left to <c>Required</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ApplicationVersionAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is not string version || InstallationVersion.TryParse(version, out _);
}
