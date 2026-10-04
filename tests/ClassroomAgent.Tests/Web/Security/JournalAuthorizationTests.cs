using System.Net;
using ClassroomAgent.Tests.TestInfrastructure;
using Actor = ClassroomAgent.Tests.TestInfrastructure.JournalHostExtensions.Actor;

namespace ClassroomAgent.Tests.Web.Security;

/// <summary>US-025 AC-010: the journal is open to an Admin and a Dean signed in, to nobody else (api-design §3–§4; TC-5).</summary>
public sealed class JournalAuthorizationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task Anonymous_IsSentToSignIn_AndNothingHappens()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.Anonymous, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);
        var before = await host.TeachingFingerprintAsync(ct);

        var response = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal(SignInTestData.SignInPath, response.LocationPath);
        Assert.DoesNotContain(SeededJournal.StudentName, response.Text, StringComparison.Ordinal);
        Assert.Equal(before, await host.TeachingFingerprintAsync(ct));
    }

    [Fact]
    public async Task ADeanWithATemporaryPassword_IsSentToTheForcedChange()
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, Actor.DeanWithTemporaryPassword, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var response = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.Redirect, response.Status);
        Assert.Equal("/sign-in/change-password", response.LocationPath);
        Assert.DoesNotContain(SeededJournal.StudentName, response.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Actor.Admin)]
    [InlineData(Actor.Dean)]
    public async Task AnAdminAndADean_SeeTheJournal(Actor actor)
    {
        var ct = TestContext.Current.CancellationToken;
        var (host, client) = await JournalHostExtensions.StartAsync(database, actor, ct);
        await using var _host = host;
        var seeded = await host.SeedJournalAsync(ct);

        var page = await client.GetAsync(JournalTestData.SeptemberUrl(seeded.CourseId), ct);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Contains(SeededJournal.StudentName, page.Text, StringComparison.Ordinal);
    }
}
