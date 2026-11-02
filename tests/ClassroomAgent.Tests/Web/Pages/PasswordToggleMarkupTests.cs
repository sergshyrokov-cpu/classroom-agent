using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using static ClassroomAgent.Tests.TestInfrastructure.PasswordFieldMarkup;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Pages;

/// <summary>
/// US-069 AC-005, AC-006, AC-007, AC-008 on the installation's six password fields (spec FR-005, FR-007): what the
/// server renders. What a browser does with it is the script scan (<c>PasswordToggleSourceTests</c>), SECURITY_REVIEW
/// and the manual walk.
/// </summary>
public sealed class PasswordToggleMarkupTests(PostgreSqlFixture database)
{
    private const string DeanSignIn = "dean-sign-in";

    private const string ForcedChange = "forced-change";

    private const string OwnPassword = "own-password";

    private const string DeanAccounts = "dean-accounts";

    private static readonly ExpectedField CurrentPassword = new("currentPassword", "current-password", null);

    private static readonly ExpectedField SignInPassword = new("password", "current-password", null);

    private static readonly ExpectedField NewPassword = new("newPassword", "new-password", null);

    private static readonly ExpectedField TemporaryPassword = new("temporaryPassword", "new-password", null);

    public static TheoryData<string> Pages => new(DeanSignIn, ForcedChange, OwnPassword, DeanAccounts);

    /// <summary>FR-005 table; the Dean-accounts page holds a row field per Dean and the create form's field.</summary>
    private static ExpectedField[] FieldsOf(string page) => page switch
    {
        DeanSignIn => [SignInPassword],
        ForcedChange => [NewPassword],
        OwnPassword => [CurrentPassword, NewPassword],
        DeanAccounts => [TemporaryPassword, TemporaryPassword, TemporaryPassword],
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };

    /// <summary>AC-008: what each form holding a password field sends today, in order of the forms on the page.</summary>
    private static string[][] FormsOf(string page) => page switch
    {
        DeanSignIn => [[Html.AntiforgeryFieldName, "email", "password"]],
        ForcedChange => [[Html.AntiforgeryFieldName, "newPassword"]],
        OwnPassword => [[Html.AntiforgeryFieldName, "currentPassword", "newPassword"]],
        DeanAccounts =>
        [
            [Html.AntiforgeryFieldName, "temporaryPassword"],
            [Html.AntiforgeryFieldName, "temporaryPassword"],
            [Html.AntiforgeryFieldName, "email", "temporaryPassword"],
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
    };

    private async Task<(InstallationTestHost Host, FormClient Client, string Path)> OpenAsync(string page, CancellationToken ct)
    {
        switch (page)
        {
            case DeanSignIn:
                {
                    var host = await InstallationTestHost.StartAsync(database, ct);
                    return (host, host.CreateClient(), DeanAccountTestData.Paths.SignIn);
                }

            case ForcedChange:
                {
                    var host = await InstallationTestHost.StartAsync(database, ct);
                    await host.InsertDeanAsync(ct);
                    var client = host.CreateClient();
                    await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TemporaryPassword, ct);
                    return (host, client, DeanAccountTestData.Paths.ForcedChange);
                }

            case OwnPassword:
                {
                    var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
                    return (host, client, DeanAccountTestData.Paths.OwnPassword);
                }

            case DeanAccounts:
                {
                    var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
                    await host.InsertDeanAsync(ct);
                    await host.InsertDeanAsync(ct, DeanAccountTestData.SecondDeanEmail);
                    return (host, client, DeanAccountTestData.Paths.Deans);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(page), page, null);
        }
    }

    /// <summary>
    /// AC-005, AC-006, FR-005.1…FR-005.3: every password input of the page sits in a wrapper, first, followed by the
    /// control; it keeps its name and <c>autocomplete</c>, has no <c>value</c> and has spell checking and
    /// auto-capitalisation off; the control is a hidden <c>type="button"</c> with the "show" name, no field attribute,
    /// no id and no visible word. The Dean-accounts page proves the row field repeats without any element id.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public async Task EveryPasswordField_CarriesTheControl(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, path) = await OpenAsync(name, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var expected = FieldsOf(name);
        Assert.Equal(expected.Length, PasswordInputs(page.Body).Count);
        var fields = Fields(page.Body);
        Assert.Equal(expected.Length, fields.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            AssertField(fields[i], expected[i], "uk");
        }
    }

    /// <summary>FR-005.1: the installation's <c>label</c> keeps wrapping field and control, so a press on its text still focuses the input.</summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public async Task TheLabel_KeepsWrappingFieldAndControl(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, path) = await OpenAsync(name, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(path, ct);

        Assert.Equal(FieldsOf(name).Length, LabelsHoldingAField(page.Body).Count);
    }

    /// <summary>AC-007, FR-006.1, FR-007: without a script nothing marks a field ready and no control can show.</summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public async Task WithoutTheScript_NoFieldIsReady_AndEveryControlIsHidden(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, path) = await OpenAsync(name, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(path, ct);

        Assert.DoesNotContain(ReadyClass, page.Body, StringComparison.Ordinal);
        var fields = Fields(page.Body);
        Assert.NotEmpty(fields);
        Assert.All(fields, f => Assert.True(HasAttribute(f.Control, "hidden"), f.Control));
    }

    /// <summary>
    /// AC-008, FR-005.2: the control adds no field to any form. Each form holding a password field names exactly what it
    /// names today, and the control's <c>type="button"</c> and absent <c>form</c> are asserted in the markup test. Green
    /// before and after implementation by design: it fails if the control ever gains a name.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public async Task TheControl_AddsNoFieldToAnyForm(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, path) = await OpenAsync(name, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(path, ct);

        var forms = FormsWithAPasswordField(page.Body);
        var expected = FormsOf(name);
        Assert.Equal(expected.Length, forms.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], NamedControls(forms[i]));
        }
    }

    /// <summary>The check above is not vacuous: a control that gained a name is listed.</summary>
    [Fact]
    public void TheFormScan_ListsANamedButton()
    {
        const string form = """
            <form><input type="password" name="password" /><button type="button" name="reveal"></button></form>
            """;

        Assert.Equal(["password", "reveal"], NamedControls(Assert.Single(FormsWithAPasswordField(form))));
    }

    /// <summary>AC-008, FR-005.5: the page references the same-host script once, deferred.</summary>
    [Theory]
    [MemberData(nameof(Pages))]
    public async Task ThePage_ReferencesTheScriptOnce_Deferred(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, path) = await OpenAsync(name, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(path, ct);

        var script = Assert.Single(ScriptReferences(page.Body));
        Assert.True(HasAttribute(script, "defer"), script);
    }

    /// <summary>FR-005.5: not from the layout — a page with no password field does not load it.</summary>
    [Fact]
    public async Task APageWithNoPasswordField_DoesNotLoadTheScript()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Dean, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.GetAsync(SignInTestData.LandingPath, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Empty(ScriptReferences(page.Body));
    }

    /// <summary>AC-010, FR-005.4: in English the control carries the English names.</summary>
    [Theory]
    [InlineData(Actor.Dean, DeanAccountTestData.Paths.OwnPassword)]
    [InlineData(Actor.Admin, DeanAccountTestData.Paths.Deans)]
    public async Task InEnglish_TheControlCarriesTheEnglishNames(Actor actor, string path)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        using var _client = client;
        await client.ChooseLanguageAsync(SignInTestData.LandingPath, UiLanguageTestData.English, ct);

        var page = await client.GetAsync(path, ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        var fields = Fields(page.Body);
        Assert.NotEmpty(fields);
        Assert.All(fields, f =>
        {
            Assert.Equal(ShowName("en"), Value(f.Control, "aria-label"));
            Assert.Contains(HideName("en"), DataValues(f.Control));
        });
    }

    /// <summary>
    /// Spec Error Handling: the form shown after a refused sign-in, which answers by a redirect back to it, starts hidden,
    /// with the control reset and no value.
    /// </summary>
    [Fact]
    public async Task AfterARefusedSignIn_TheFieldStartsHidden()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);
        await host.InsertDeanAsync(ct);
        using var client = host.CreateClient();

        var refused = await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.WrongPassword, ct);
        Assert.Equal(DeanAccountTestData.Paths.SignIn, refused.LocationPath);

        var page = await client.GetAsync(DeanAccountTestData.Paths.SignIn, ct);

        Assert.Single(PasswordInputs(page.Body));
        AssertField(Assert.Single(Fields(page.Body)), SignInPassword, "uk");
    }

    /// <summary>Spec Error Handling: the same for the Admin's create form, whose email is kept and whose password is not.</summary>
    [Fact]
    public async Task AfterARefusedCreation_TheFieldStartsHidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client, _) = await DeanAccountHostExtensions.StartSignedInAsync(database, ct);
        await using var _host = host;
        using var _client = client;

        var page = await client.CreateDeanAsync(DeanAccountTestData.DeanEmail, DeanAccountTestData.TooShortPassword, ct);

        Assert.Equal(HttpStatusCode.BadRequest, page.Status);
        Assert.Single(PasswordInputs(page.Body));
        AssertField(Assert.Single(Fields(page.Body)), TemporaryPassword, "uk");
    }

    /// <summary>AC-008, SC-4, FR-008.1: the script and both icons are served anonymously, with their content types.</summary>
    [Theory]
    [InlineData(ScriptPath, "javascript")]
    [InlineData(EyeIconPath, "image/svg+xml")]
    [InlineData(EyeOffIconPath, "image/svg+xml")]
    public async Task AStaticFile_IsServedWithItsContentType(string path, string contentType)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.StartAsync(database, ct);

        var response = await host.SendPublicAsync("GET", path, ct);

        Assert.Equal(HttpStatusCode.OK, response.Status);
        Assert.Contains(contentType, response.ContentType ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
