using System.Text.RegularExpressions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives the US-010 instruction over HTTP: a host whose Control Plane answers the Admin login check
/// "allowed", a signed-in Admin, and the one <c>GET</c> of US-010 openapi. Nothing here reaches Google — this
/// Story makes no Google call in any mode (spec S-05).
/// </summary>
public static partial class ConnectionInstructionHostExtensions
{
    /// <summary>
    /// A started installation in the given legitimacy state with an Admin signed in. The default seeds a
    /// recent successful check, so both the domain and the client ID are known and the installation is not
    /// read-only.
    /// </summary>
    public static Task<(InstallationTestHost Host, FormClient Client, ScriptedHttpHandler Channel)> StartSignedInAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly) =>
        WorkspaceConnectionHostExtensions.StartSignedInAsync(database, cancellationToken, cause);

    /// <summary>Opens the instruction page.</summary>
    public static Task<PageResponse> OpenInstructionAsync(this FormClient client, CancellationToken cancellationToken) =>
        client.GetAsync(ConnectionInstructionTestData.Path, cancellationToken);

    /// <summary>
    /// The text the Admin hands over (spec FR-008): the content of
    /// <c>&lt;pre id="connection-instruction"&gt;</c>. A <c>pre</c> because the text is copied verbatim and
    /// must keep its line breaks, and because it cannot nest — so what this returns is exactly what a person
    /// would paste. Character references are decoded and any tag inside is stripped.
    /// </summary>
    public static string HandedOverText(PageResponse page)
    {
        var match = InstructionElement().Match(page.Body);
        Assert.True(
            match.Success,
            $"The page carries no <pre id=\"{ConnectionInstructionTestData.InstructionElementId}\"> (spec FR-008, AC-009).");
        return System.Net.WebUtility.HtmlDecode(Tags().Replace(match.Groups[1].Value, " "));
    }

    /// <summary>True when the page carries an element with the given id at all.</summary>
    public static bool HasElement(PageResponse page, string id) =>
        Regex.IsMatch(page.Body, "id=\"" + Regex.Escape(id) + "\"", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    /// <summary>
    /// The <c>src</c> of every script the page references. Spec I-5 wants the copy affordance in a static file,
    /// so this is what a test compares against the inline scripts below.
    /// </summary>
    public static IReadOnlyList<string> ScriptSources(PageResponse page) =>
        ScriptWithSource().Matches(page.Body).Select(m => m.Groups[1].Value).ToList();

    /// <summary>
    /// Every inline script body on the page. Spec I-5 requires this to be empty: a Content-Security-Policy
    /// added later must not need <c>unsafe-inline</c>.
    /// </summary>
    public static IReadOnlyList<string> InlineScripts(PageResponse page) =>
        ScriptElement().Matches(page.Body)
            .Where(m => !m.Groups[1].Value.Contains("src=", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Groups[2].Value.Trim())
            .Where(body => body.Length > 0)
            .ToList();

    /// <summary>The row counts of every table of the installation database, for "writes nothing" (spec FR-010).</summary>
    public static async Task<IReadOnlyDictionary<string, long>> TableRowCountsAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in await host.TableNamesAsync(cancellationToken))
        {
            if (table.StartsWith("__", StringComparison.Ordinal))
            {
                continue;
            }

            counts[table] = await host.ScalarAsync<long>("SELECT count(*) FROM " + table, cancellationToken);
        }

        return counts;
    }

    [GeneratedRegex(
        "<pre[^>]*id=\"" + ConnectionInstructionTestData.InstructionElementId + "\"[^>]*>(.*?)</pre>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InstructionElement();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex("<script[^>]*\\ssrc=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptWithSource();

    [GeneratedRegex("<script([^>]*)>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptElement();
}
