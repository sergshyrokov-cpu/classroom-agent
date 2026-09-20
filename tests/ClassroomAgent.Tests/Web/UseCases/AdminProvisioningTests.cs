using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-008 AC-007: an approved Admin gets an <c>AppUser</c> at the first sign-in — role Admin, sign-in method
/// Google, no password at all — the time of the last successful sign-in is recorded, and a second sign-in
/// reuses the same row (spec FR-003, FR-010, FR-011; S-04; BR-010, BR-011; db-design 3.3).
/// </summary>
public sealed class AdminProvisioningTests(PostgreSqlFixture database)
{
    private static ScriptedHttpHandler Allowed() =>
        ScriptedHttpHandler.Json(HttpStatusCode.OK, AdminLoginCheckTestData.AnswerJson(true));

    [Fact]
    public async Task FirstSignIn_CreatesTheAdminAppUser_WithNoPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var user = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal(SignInTestData.AdminEmail, user.Email);
        Assert.Equal(SignInTestData.AdminEmail, user.NormalizedEmail);
        Assert.Equal("admin", user.Role);
        Assert.Equal("google", user.SignInMethod);
        Assert.Null(user.PasswordHash);
        Assert.False(user.IsDisabled);
        Assert.Equal(0, user.AccessFailedCount);
        Assert.Null(user.LockoutEnd);
    }

    /// <summary>AC-007, FR-011: the language is the school default; Ukrainian when the setting is absent.</summary>
    [Fact]
    public async Task FirstSignIn_SetsTheSchoolDefaultLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        await ReadOnlyModeHost.SeedAsync(host, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        host.Settings[InstallationConfigurationKeys.DefaultLanguage] = "en";
        host.ControlPlaneHandler = channel;
        host.Start();

        await host.SignInWithGoogleAsync(ct);

        var user = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal("en", user.UiLanguage);
    }

    [Fact]
    public async Task WithoutTheLanguageSetting_TheAppUserIsUkrainian()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var user = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal("uk", user.UiLanguage);
    }

    /// <summary>AC-007, PC-11: the stamp is the injectable clock's now, so retention counts from it.</summary>
    [Fact]
    public async Task FirstSignIn_RecordsTheTimeOfTheLastSuccessfulSignIn()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);

        var user = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal(InstallationTestHost.DefaultStart, user.LastSuccessfulSignInAt);
    }

    /// <summary>AC-007: a second sign-in reuses the row and moves only the stamp.</summary>
    [Fact]
    public async Task SecondSignIn_ReusesTheRow_AndOnlyMovesTheStamp()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.SignInWithGoogleAsync(ct);
        var created = Assert.Single(await host.AppUsersAsync(ct));
        host.Time.Advance(TimeSpan.FromHours(26));

        await host.SignInWithGoogleAsync(ct);

        var user = Assert.Single(await host.AppUsersAsync(ct));
        Assert.Equal(created.Id, user.Id);
        Assert.Equal(created.CreatedAt, user.CreatedAt);
        Assert.Equal(InstallationTestHost.DefaultStart + TimeSpan.FromHours(26), user.LastSuccessfulSignInAt);
        Assert.Equal(created.Email, user.Email);
        Assert.Equal(created.Role, user.Role);
    }

    /// <summary>AC-007: two Admins of the same school get one row each.</summary>
    [Fact]
    public async Task TwoApprovedAdmins_GetOneRowEach()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);

        await host.SignInWithGoogleAsync(ct);
        host.Google.Email = SignInTestData.SecondAdminEmail;
        await host.SignInWithGoogleAsync(ct);

        var users = await host.AppUsersAsync(ct);
        Assert.Equal(
            new[] { SignInTestData.AdminEmail, SignInTestData.SecondAdminEmail },
            users.Select(u => u.Email).Order(StringComparer.Ordinal));
        Assert.All(users, u => Assert.Equal("admin", u.Role));
    }

    /// <summary>AC-007: the uniqueness is the database's, not a check before insert (db-design 3.1).</summary>
    [Fact]
    public async Task ASecondRowForTheSameEmail_IsImpossible()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        await host.SignInWithGoogleAsync(ct);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => host.InsertAppUserAsync(ct));

        Assert.Contains("uq_app_user_normalized_email", Text(failure), StringComparison.OrdinalIgnoreCase);
        Assert.Single(await host.AppUsersAsync(ct));
    }

    /// <summary>AC-007, I-10: a concurrent first sign-in becomes a re-read, never an error to the user.</summary>
    [Fact]
    public async Task TwoSimultaneousFirstSignIns_CreateOneRow_AndNeitherFails()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        host.UseGoogleStub();

        using var first = host.CreateClient();
        using var second = host.CreateClient();
        var firstStart = await first.StartGoogleSignInAsync(ct);
        var secondStart = await second.StartGoogleSignInAsync(ct);
        var callbacks = await Task.WhenAll(
            first.CompleteGoogleCallbackAsync(InstallationSignInExtensions.StateOf(firstStart), ct),
            second.CompleteGoogleCallbackAsync(InstallationSignInExtensions.StateOf(secondStart), ct));

        Assert.All(callbacks, c => Assert.Equal(SignInTestData.LandingPath, c.LocationPath));
        Assert.Single(await host.AppUsersAsync(ct));
    }

    /// <summary>AC-007, S-04, AD-8: the Identity-shaped fields never reach a response; only the email and the role do.</summary>
    [Fact]
    public async Task TheStoredStamps_NeverReachAResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var channel = Allowed();
        await using var host = await InstallationTestHost.StartWithControlPlaneHttpAsync(database, channel, ct);
        var (client, callback) = await host.SignInWithGoogleAsync(ct);
        var landing = await client.GetAsync(SignInTestData.LandingPath, ct);

        var user = Assert.Single(await host.AppUsersAsync(ct));

        foreach (var body in new[] { callback.Body, landing.Body })
        {
            Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password_hash", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("securityStamp", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("concurrencyStamp", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("accessFailedCount", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"", body, StringComparison.Ordinal);
        }
    }

    private static string Text(Exception exception)
    {
        var text = new System.Text.StringBuilder();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine(current.Message);
        }

        return text.ToString();
    }
}
