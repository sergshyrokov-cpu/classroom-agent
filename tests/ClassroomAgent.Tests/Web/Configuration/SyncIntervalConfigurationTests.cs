using ClassroomAgent.Tests.TestInfrastructure;
using ClassroomAgent.Web.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Configuration;

/// <summary>
/// US-013 spec FR-013, VR-001: the optional <c>Sync:IntervalMinutes</c> setting, alongside the settings
/// US-005 and US-008 already make required or optional. Absent or blank yields the one-hour default; present,
/// it is a whole number of minutes from 1 to 1440; anything else stops the start with
/// <see cref="InstallationSettingException"/> and never logs the rejected value (SC-10).
/// </summary>
public sealed class SyncIntervalConfigurationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AbsentSetting_ResolvesToTheDefaultInterval()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);

        host.Start();

        var settings = host.Services.GetRequiredService<SyncScheduleSettings>();
        Assert.Equal(SyncTestData.DefaultInterval, settings.Interval);
    }

    [Fact]
    public async Task BlankSetting_ResolvesToTheDefaultInterval()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationSettingsReader.SyncIntervalKey] = "   ";

        host.Start();

        var settings = host.Services.GetRequiredService<SyncScheduleSettings>();
        Assert.Equal(SyncTestData.DefaultInterval, settings.Interval);
    }

    [Fact]
    public async Task AValidSetting_ResolvesToThatInterval()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationSettingsReader.SyncIntervalKey] = "15";

        host.Start();

        var settings = host.Services.GetRequiredService<SyncScheduleSettings>();
        Assert.Equal(TimeSpan.FromMinutes(15), settings.Interval);
    }

    /// <summary>VR-001: not an integer, a fraction, zero, negative, above a day, or carrying a sign or a space.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1441")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("+5")]
    [InlineData(" 5 ")]
    public async Task InvalidSetting_HostDoesNotStart_NamingTheKeyNotTheValue(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationSettingsReader.SyncIntervalKey] = value;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        var text = ExceptionText(exception);
        Assert.Contains(InstallationSettingsReader.SyncIntervalKey, text, StringComparison.Ordinal);
        if (value.Trim().Length >= 3)
        {
            Assert.DoesNotContain(value, text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1440")]
    public async Task BoundaryValidSetting_HostStarts(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[InstallationSettingsReader.SyncIntervalKey] = value;

        host.Start();

        var settings = host.Services.GetRequiredService<SyncScheduleSettings>();
        Assert.Equal(TimeSpan.FromMinutes(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture)), settings.Interval);
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
