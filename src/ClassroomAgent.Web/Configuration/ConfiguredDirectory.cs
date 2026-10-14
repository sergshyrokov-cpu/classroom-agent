namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// US-047 spec FR-004, VR-001: a directory from configuration — an absolute value is used as given, any other value
/// resolves against the host's content root, never against the process working directory.
/// </summary>
public static class ConfiguredDirectory
{
    /// <summary>
    /// A drive-relative (<c>C:logs</c>) or root-relative (<c>\logs</c>) value is not fully qualified, so it is treated
    /// like any relative value: its drive or root prefix is dropped and the rest lands under the content root (I-5).
    /// </summary>
    public static string Resolve(string configured, string contentRoot)
    {
        if (Path.IsPathFullyQualified(configured))
        {
            return configured;
        }

        var relative = configured[Path.GetPathRoot(configured.AsSpan()).Length..];
        return Path.GetFullPath(Path.Join(contentRoot, relative));
    }
}
