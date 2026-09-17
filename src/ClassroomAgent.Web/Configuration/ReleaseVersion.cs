using System.Reflection;
using System.Text.RegularExpressions;

namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// The installation's release version <c>MAJOR.MINOR.PATCH</c> reported on every check (US-005 spec I-7;
/// api-design §6): the informational version without pre-release or build metadata, or the assembly version.
/// </summary>
public static partial class ReleaseVersion
{
    public static string Of(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (informational?.Split('+', '-')[0] is { } core && Shape().IsMatch(core))
        {
            return core;
        }

        var version = assembly.GetName().Version ?? new Version(0, 0, 0);
        return $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }

    [GeneratedRegex(@"\A(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\.(0|[1-9][0-9]{0,5})\z", RegexOptions.CultureInvariant)]
    private static partial Regex Shape();
}
