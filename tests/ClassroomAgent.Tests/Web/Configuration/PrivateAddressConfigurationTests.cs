using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Configuration;

/// <summary>
/// US-006 AC-001: the private port's address is a required installation setting — an IP literal or the
/// explicit `*` — and a refusal names the key, never the value (spec FR-001, VR-003; api-design §8;
/// `trebovaniya.md` v75, v76; DC-6, SC-9, SC-10).
/// </summary>
public sealed class PrivateAddressConfigurationTests(PostgreSqlFixture database)
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("[fd00::5]")]
    [InlineData("fd00::5")]
    [InlineData("*")]
    public async Task ValidAddress_HostStarts(string address)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[PushTestData.PrivateAddressKey] = address;

        host.Start();

        Assert.NotNull(host.Services);
    }

    [Fact]
    public async Task MissingAddress_HostDoesNotStart_NamingTheKey()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[PushTestData.PrivateAddressKey] = null;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(PushTestData.PrivateAddressKey, ExceptionText(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("localhost")]
    [InlineData("server.private.test")]
    [InlineData("10.0.0.5:8081")]
    [InlineData("http://10.0.0.5")]
    [InlineData("0.0.0.0/24")]
    [InlineData("**")]
    [InlineData("+")]
    [InlineData("999.0.0.1")]
    public async Task InvalidAddress_HostDoesNotStart_NamingTheKeyWithoutTheValue(string address)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[PushTestData.PrivateAddressKey] = address;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        var text = ExceptionText(exception);
        Assert.Contains(PushTestData.PrivateAddressKey, text, StringComparison.Ordinal);
        if (!string.IsNullOrWhiteSpace(address))
        {
            Assert.DoesNotContain(address, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task InvalidAddress_IsNotWrittenToTheLog()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[PushTestData.PrivateAddressKey] = "203.0.113.9:8081";

        Assert.ThrowsAny<Exception>(host.Start);

        var logs = await host.ReadLogFilesAsync(ct);
        Assert.DoesNotContain(logs, log => log.Contains("203.0.113.9", StringComparison.Ordinal));
        Assert.Contains(logs, log => log.Contains(PushTestData.PrivateAddressKey, StringComparison.Ordinal));
    }

    private static string ExceptionText(Exception exception)
    {
        var text = new System.Text.StringBuilder();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            text.AppendLine(current.Message);
        }

        return text.ToString();
    }
}
