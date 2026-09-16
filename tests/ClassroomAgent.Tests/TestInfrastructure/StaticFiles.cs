namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>Finds the Control Plane's static files (SC-4 "Static files", api-design §4).</summary>
public static class StaticFiles
{
    private static readonly string[] Folders = ["css", "js", "images"];

    /// <summary>The URL path of one file present in the static files directory.</summary>
    public static string AnyFile()
    {
        var webRoot = Path.Combine(RepositoryRoot(), "src", "ClassroomAgent.ControlPlane", "wwwroot");
        foreach (var folder in Folders)
        {
            var directory = Path.Combine(webRoot, folder);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var file = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).FirstOrDefault();
            if (file is not null)
            {
                return "/" + Path.GetRelativePath(webRoot, file).Replace('\\', '/');
            }
        }

        Assert.Fail("The Control Plane has no static file under wwwroot/css, wwwroot/js or wwwroot/images.");
        return string.Empty;
    }

    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ClassroomAgent.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("ClassroomAgent.sln not found above the test output.");
    }
}
