using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-068 AC-002, spec FR-001, FR-010.1, OD-001 a): each host vendors Bootstrap 5.3 in <c>wwwroot/lib/bootstrap/</c> —
/// the unmodified minified stylesheet, its MIT licence and a version note — and nothing else; the stylesheet matches
/// the integrity hash the note records, and both hosts carry the same bytes.
/// </summary>
public sealed partial class VendoredBootstrapTests
{
    public static TheoryData<string> Hosts => new(Styling.Installation, Styling.ControlPlane);

    /// <summary>AC-002, FR-001.1, FR-001.4: exactly the stylesheet, the licence and the version note — no script, map or font.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheLibraryFolder_HoldsExactlyTheThreeFiles(string host)
    {
        var folder = Styling.LibraryFolder(host);
        Assert.True(Directory.Exists(folder), $"Missing folder: src/{host}/wwwroot/lib/bootstrap");

        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(folder, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(Styling.LibraryFiles, files);
    }

    /// <summary>AC-002, FR-001.4: no <c>*.js</c> or <c>*.map</c> anywhere under the host's <c>wwwroot/lib/bootstrap/</c>.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheLibraryFolder_HoldsNoScriptAndNoSourceMap(string host)
    {
        var folder = Styling.LibraryFolder(host);
        Assert.True(Directory.Exists(folder), $"Missing folder: src/{host}/wwwroot/lib/bootstrap");

        var offenders = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>AC-002, FR-001.1: <c>LICENSE.txt</c> is the MIT licence naming Bootstrap.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheLicence_IsTheMitLicenceOfBootstrap(string host)
    {
        var licence = Styling.ReadRequired(Styling.FileOf(host, Styling.LicensePath));

        Assert.Contains("MIT License", licence, StringComparison.Ordinal);
        Assert.Contains("Bootstrap", licence, StringComparison.Ordinal);
        Assert.Contains("Permission is hereby granted, free of charge", licence, StringComparison.Ordinal);
    }

    /// <summary>AC-002, FR-001.3: the version note names a 5.3.x version, the release archive URL, a date, a sha384 hash and says the file is unmodified.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheVersionNote_RecordsVersionArchiveDateHashAndUnmodified(string host)
    {
        var note = Styling.ReadRequired(Styling.FileOf(host, Styling.VersionPath));

        var version = VersionIn(note);
        var url = ArchiveUrl().Match(note);
        Assert.True(url.Success, "VERSION.txt names no https://github.com/twbs/bootstrap/releases/… archive URL.");
        Assert.Contains("v" + version, url.Value, StringComparison.Ordinal);
        Assert.Matches(IsoDate(), note);
        Assert.NotNull(HashIn(note));
        Assert.Contains("unmodified", note, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>AC-002, FR-001.2, §6: the SHA-384 of <c>bootstrap.min.css</c>, in base64, equals the hash the note records.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheStylesheet_MatchesTheRecordedIntegrityHash(string host)
    {
        var note = Styling.ReadRequired(Styling.FileOf(host, Styling.VersionPath));
        var stylesheet = Styling.FileOf(host, Styling.BootstrapPath);
        Assert.True(File.Exists(stylesheet), $"Missing file: src/{host}/wwwroot{Styling.BootstrapPath}");

        var actual = "sha384-" + Convert.ToBase64String(SHA384.HashData(File.ReadAllBytes(stylesheet)));

        Assert.Equal(HashIn(note), actual);
    }

    /// <summary>AC-002: the stylesheet's own header comment names the version the note records.</summary>
    [Theory]
    [MemberData(nameof(Hosts))]
    public void TheStylesheetHeader_NamesTheRecordedVersion(string host)
    {
        var note = Styling.ReadRequired(Styling.FileOf(host, Styling.VersionPath));
        var stylesheet = Styling.ReadRequired(Styling.FileOf(host, Styling.BootstrapPath));

        var header = HeaderVersion().Match(stylesheet[..Math.Min(stylesheet.Length, 500)]);

        Assert.True(header.Success, "bootstrap.min.css has no 'Bootstrap v5.3.x' header comment.");
        Assert.Equal(VersionIn(note), header.Groups["version"].Value);
    }

    /// <summary>AC-002, FR-001.1, FR-010.1: the two hosts carry byte-identical copies of all three files.</summary>
    [Fact]
    public void BothHosts_CarryTheSameBytes()
    {
        foreach (var file in Styling.LibraryFiles)
        {
            var installation = Path.Combine(Styling.LibraryFolder(Styling.Installation), file);
            var controlPlane = Path.Combine(Styling.LibraryFolder(Styling.ControlPlane), file);
            Assert.True(File.Exists(installation), $"Missing file: installation lib/bootstrap/{file}");
            Assert.True(File.Exists(controlPlane), $"Missing file: Control Plane lib/bootstrap/{file}");

            Assert.True(
                File.ReadAllBytes(installation).AsSpan().SequenceEqual(File.ReadAllBytes(controlPlane)),
                $"lib/bootstrap/{file} differs between the hosts.");
        }
    }

    private static string VersionIn(string note)
    {
        var match = Version().Match(note);
        Assert.True(match.Success, "VERSION.txt names no 5.3.x version.");
        return match.Value;
    }

    private static string? HashIn(string note)
    {
        var match = Sha384().Match(note);
        Assert.True(match.Success, "VERSION.txt records no sha384-… integrity hash.");
        return match.Value;
    }

    [GeneratedRegex(@"(?<![\d.])5\.3\.\d+(?![\d.])")]
    private static partial Regex Version();

    [GeneratedRegex(@"https://github\.com/twbs/bootstrap/releases/\S+")]
    private static partial Regex ArchiveUrl();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"sha384-[A-Za-z0-9+/]{64}")]
    private static partial Regex Sha384();

    [GeneratedRegex(@"Bootstrap\s+v(?<version>5\.3\.\d+)")]
    private static partial Regex HeaderVersion();
}
