namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-047: the folders one host build may write into — a private temporary root (an absolute path, or the content
/// root of a console start), and a uniquely named relative directory that a test resolves against the application
/// folder or the working directory. Disposal removes all of them, wherever the host put its files.
/// </summary>
public sealed class HostFolders : IDisposable
{
    public HostFolders()
    {
        var id = Guid.NewGuid().ToString("N");
        Root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "classroom-agent-tests", id)).FullName;
        RelativeName = "us047-" + id;
    }

    /// <summary>An absolute directory of this test alone.</summary>
    public string Root { get; }

    /// <summary>A relative directory name no other test uses.</summary>
    public string RelativeName { get; }

    /// <summary>The folder the application is installed in — for the test process, the test output folder.</summary>
    public static string ApplicationFolder => Normalize(AppContext.BaseDirectory);

    public static string WorkingDirectory => Normalize(Directory.GetCurrentDirectory());

    /// <summary>A relative directory value under <see cref="RelativeName"/>, as configuration would carry it.</summary>
    public string Relative(string leaf) => Path.Combine(RelativeName, leaf);

    /// <summary>An absolute directory under <see cref="Root"/>.</summary>
    public string Absolute(string leaf) => Path.Combine(Root, leaf);

    public static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static void AssertSamePath(string expected, string actual) =>
        Assert.Equal(Normalize(expected), Normalize(actual), ignoreCase: OperatingSystem.IsWindows());

    /// <summary>The files directly in a directory matching a pattern; none when it does not exist.</summary>
    public static IReadOnlyList<string> Files(string directory, string pattern) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : [];

    public void Dispose()
    {
        Delete(Root);
        Delete(Path.Combine(ApplicationFolder, RelativeName));
        Delete(Path.Combine(WorkingDirectory, RelativeName));
    }

    private static void Delete(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
