using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Configuration;

/// <summary>
/// US-005 AC-001: the installation starts only with a valid installation id, Control Plane address,
/// private port and connection string; a refusal names the setting, never its value (spec FR-001, VR-001,
/// I-1, I-2; api-design §10).
/// </summary>
public sealed class InstallationConfigurationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AllRequiredSettingsValid_HostStarts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        host.Start();

        Assert.NotNull(host.Services);
    }

    [Fact]
    public async Task OnlyTheSettingsOfThisStoryAreRequired_NoTimeZoneRetentionOrLanguage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        Assert.Equal(5, host.Settings.Count);

        host.Start();

        Assert.NotNull(host.Services);
    }

    [Theory]
    [InlineData(InstallationConfigurationKeys.InstallationId)]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress)]
    [InlineData(InstallationConfigurationKeys.PrivatePort)]
    [InlineData(InstallationConfigurationKeys.ConnectionString)]
    public async Task MissingRequiredSetting_HostDoesNotStart_NamingTheKey(string key)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[key] = null;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(key, ExceptionText(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstallationConfigurationKeys.InstallationId, "")]
    [InlineData(InstallationConfigurationKeys.InstallationId, "   ")]
    [InlineData(InstallationConfigurationKeys.InstallationId, "school-one-installation")]
    [InlineData(InstallationConfigurationKeys.InstallationId, "{3f1c2a8e-5b7d-4c9e-a1f0-2d6b8e4c7a15}")]
    [InlineData(InstallationConfigurationKeys.InstallationId, "3f1c2a8e5b7d4c9ea1f02d6b8e4c7a15")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "control-plane.internal")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "/service")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "http://control-plane.internal")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "ftp://control-plane.internal")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "https://owner:secret@control-plane.internal")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "https://control-plane.internal/?token=x")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "https://control-plane.internal/#part")]
    [InlineData(InstallationConfigurationKeys.PrivatePort, "")]
    [InlineData(InstallationConfigurationKeys.PrivatePort, "0")]
    [InlineData(InstallationConfigurationKeys.PrivatePort, "-1")]
    [InlineData(InstallationConfigurationKeys.PrivatePort, "65536")]
    [InlineData(InstallationConfigurationKeys.PrivatePort, "eighty")]
    [InlineData(InstallationConfigurationKeys.PrivatePort, "8081.5")]
    [InlineData(InstallationConfigurationKeys.ConnectionString, "")]
    public async Task InvalidSetting_HostDoesNotStart_NamingTheKeyNotTheValue(string key, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[key] = value;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        var text = ExceptionText(exception);
        Assert.Contains(key, text, StringComparison.Ordinal);
        if (value.Trim().Length >= 3)
        {
            Assert.DoesNotContain(value, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task PrivatePortEqualToAPublicPort_HostDoesNotStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationConfigurationKeys.Urls] = "https://localhost:8443;https://localhost:8081";

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(InstallationConfigurationKeys.PrivatePort, ExceptionText(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(InstallationConfigurationKeys.InstallationId, "3F1C2A8E-5B7D-4C9E-A1F0-2D6B8E4C7A15")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "https://control-plane.internal:9443")]
    [InlineData(InstallationConfigurationKeys.ControlPlaneAddress, "https://10.0.0.5/")]
    [InlineData(InstallationConfigurationKeys.PrivatePort, "1")]
    [InlineData(InstallationConfigurationKeys.PrivatePort, "65535")]
    public async Task BoundaryValidSetting_HostStarts(string key, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[key] = value;

        host.Start();

        Assert.NotNull(host.Services);
    }

    [Fact]
    public async Task InvalidSetting_IsLogged_WithTheKeyAndWithoutTheValue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        const string secretLooking = "https://owner:zz-secret-marker@control-plane.internal";
        host.Settings[InstallationConfigurationKeys.ControlPlaneAddress] = secretLooking;

        Assert.ThrowsAny<Exception>(host.Start);
        var logs = string.Join('\n', await host.ReadLogFilesAsync(ct));

        Assert.Contains(InstallationConfigurationKeys.ControlPlaneAddress, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("zz-secret-marker", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidSettings_AreNotStoredInTheDatabase()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.ControlPlane.ReplySuccess();
        host.Start();
        await host.WaitForNextCheckAtAsync(host.Time.GetUtcNow() + TimeSpan.FromHours(6), ct);

        var dump = await host.QueryAsync(
            "SELECT row_to_json(s)::text FROM legitimacy_state s",
            r => r.GetString(0),
            ct);

        var text = string.Join('\n', dump);
        Assert.DoesNotContain(host.InstallationId.ToString("D"), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("control-plane.test", text, StringComparison.Ordinal);
    }

    private static string ExceptionText(Exception exception)
    {
        var parts = new List<string>();
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            parts.Add(e.Message);
            if (e is AggregateException aggregate)
            {
                parts.AddRange(aggregate.InnerExceptions.Select(i => i.Message));
            }
        }

        return string.Join(Environment.NewLine, parts);
    }
}
