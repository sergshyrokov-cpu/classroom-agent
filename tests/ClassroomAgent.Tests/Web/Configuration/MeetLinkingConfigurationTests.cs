using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Configuration;

/// <summary>
/// US-032 FR-005, VR-005, AC-006: the optional <c>MeetLinking:MinSharePercent</c> and <c>MeetLinking:MinGapPoints</c>
/// settings. Absent, they resolve to 60 and 30; present, each is a whole number from 1 to 100; anything else — blank
/// included — stops the start with an exception that names the key and never carries the value (SC-10).
/// </summary>
public sealed class MeetLinkingConfigurationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AbsentSettings_ResolveToTheDefaultThresholds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        host.Start();

        var thresholds = host.Services.GetRequiredService<MeetLinkingThresholds>();
        Assert.Equal(MeetLinkingThresholds.Default, thresholds);
        Assert.Equal(new MeetLinkingThresholds(60, 30), thresholds);
    }

    [Fact]
    public async Task ValidSettings_ResolveToThoseThresholds()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationSettingsReader.MeetLinkingMinSharePercentKey] = "55";
        host.Settings[InstallationSettingsReader.MeetLinkingMinGapPointsKey] = "10";

        host.Start();

        Assert.Equal(new MeetLinkingThresholds(55, 10), host.Services.GetRequiredService<MeetLinkingThresholds>());
    }

    /// <summary>Only one key set: the other keeps its default.</summary>
    [Theory]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, 40, 30)]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, 60, 40)]
    public async Task OneSetting_LeavesTheOtherAtItsDefault(string key, int share, int gap)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[key] = "40";

        host.Start();

        Assert.Equal(new MeetLinkingThresholds(share, gap), host.Services.GetRequiredService<MeetLinkingThresholds>());
    }

    [Theory]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, "1")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, "100")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, "1")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, "100")]
    public async Task BoundaryValidSetting_HostStarts(string key, string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[key] = value;

        host.Start();

        var thresholds = host.Services.GetRequiredService<MeetLinkingThresholds>();
        var number = int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(
            key == InstallationSettingsReader.MeetLinkingMinSharePercentKey ? number : MeetLinkingThresholds.Default.MinSharePercent,
            thresholds.MinSharePercent);
        Assert.Equal(
            key == InstallationSettingsReader.MeetLinkingMinGapPointsKey ? number : MeetLinkingThresholds.Default.MinGapPoints,
            thresholds.MinGapPoints);
    }

    /// <summary>VR-005: out of range, not a whole number, signed or padded, or blank.</summary>
    [Theory]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, "0")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, "101")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, "-5")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, "abc")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, "1.5")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, "+5")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, " 5 ")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinSharePercentKey, "")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, "0")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, "101")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, "-5")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, "abc")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, "1.5")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, "+5")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, " 5 ")]
    [InlineData(InstallationSettingsReader.MeetLinkingMinGapPointsKey, "")]
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
