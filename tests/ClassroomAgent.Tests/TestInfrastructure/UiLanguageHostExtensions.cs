using System.Net;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives the US-039 action over HTTP on either host: open a page (for its antiforgery token and, once the
/// switcher exists, its return path), then post the choice. While the switcher does not exist yet the page still
/// carries the sign-out form's token, so a red-phase POST fails on the behaviour under test, not on the fixture.
/// </summary>
public static class UiLanguageHostExtensions
{
    /// <summary>Opens <paramref name="fromPage"/> and posts the choice with its token.</summary>
    public static async Task<PageResponse> ChooseLanguageAsync(
        this FormClient client,
        string fromPage,
        string? language,
        CancellationToken cancellationToken,
        string? returnPath = null,
        bool withToken = true,
        IEnumerable<KeyValuePair<string, string>>? extraFields = null)
    {
        await client.GetAsync(fromPage, cancellationToken);
        if (withToken && client.LastToken is null)
        {
            await client.GetAsync("/", cancellationToken);
        }

        var fields = new List<KeyValuePair<string, string>>();
        if (language is not null)
        {
            fields.Add(new(UiLanguageTestData.Fields.Language, language));
        }

        fields.Add(new(UiLanguageTestData.Fields.ReturnPath, returnPath ?? fromPage));
        fields.AddRange(extraFields ?? []);
        return await client.PostFormAsync(UiLanguageTestData.ChoosePath, fields, cancellationToken, withToken);
    }

    /// <summary>
    /// An installation with a Dean whose password is no longer temporary, signed in by password. Returns the
    /// client and the Dean's id.
    /// </summary>
    public static async Task<(FormClient Client, long DeanId)> SignInDeanAsync(
        this InstallationTestHost host,
        CancellationToken cancellationToken,
        string email = DeanAccountTestData.DeanEmail,
        bool passwordIsTemporary = false)
    {
        if (!(await host.AppUsersAsync(cancellationToken)).Any(u => u.Email == email))
        {
            await host.InsertDeanAsync(cancellationToken, email, passwordIsTemporary: passwordIsTemporary);
        }

        var client = host.CreateClient();
        var response = await client.SignInAsDeanAsync(email, DeanAccountTestData.TemporaryPassword, cancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        var id = (await host.AppUsersAsync(cancellationToken)).Single(u => u.Email == email).Id;
        return (client, id);
    }

    /// <summary>The stored language of one installation account.</summary>
    public static async Task<string> StoredLanguageAsync(
        this InstallationTestHost host,
        long accountId,
        CancellationToken cancellationToken) =>
        (await host.AppUsersAsync(cancellationToken)).Single(u => u.Id == accountId).UiLanguage;

    /// <summary>The stored security stamp of one installation account.</summary>
    public static Task<string?> SecurityStampAsync(
        this InstallationTestHost host,
        long accountId,
        CancellationToken cancellationToken) =>
        host.ScalarAsync<string>(
            "SELECT security_stamp FROM app_user WHERE id = @id",
            cancellationToken,
            ("id", accountId));
}
