using ClassroomAgent.Application.Models;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Web.Configuration;

/// <summary>
/// US-015 spec FR-012, VR-008: the required <c>Retention:Years</c> setting — the whole positive number of years
/// the purge (US-037) will use to compute a course's age (§5). Unlike <c>Sync:IntervalMinutes</c> (US-013
/// VR-001) there is no default: missing, non-numeric, zero or negative each stop the host's start, and only a
/// valid value lets it start. Mirrors <see cref="SyncIntervalConfigurationTests"/>, the US-013 precedent for a
/// setting proved at host level.
/// </summary>
public sealed class RetentionConfigurationTests(PostgreSqlFixture database)
{
    private const string RetentionYearsKey = "Retention:Years";

    [Fact]
    public async Task MissingRetentionYears_RefusesToStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings.Remove(RetentionYearsKey);

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(RetentionYearsKey, ExceptionText(exception), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonNumericRetentionYears_RefusesToStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[RetentionYearsKey] = "abc";

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(RetentionYearsKey, ExceptionText(exception), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZeroRetentionYears_RefusesToStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[RetentionYearsKey] = "0";

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(RetentionYearsKey, ExceptionText(exception), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NegativeRetentionYears_RefusesToStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[RetentionYearsKey] = "-5";

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(RetentionYearsKey, ExceptionText(exception), StringComparison.Ordinal);
    }

    /// <summary>US-037 OD-008: above 100 years the installation refuses to start, as it does for a missing N.</summary>
    [Fact]
    public async Task RetentionYearsAboveTheBound_RefusesToStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[RetentionYearsKey] = "101";

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(RetentionYearsKey, ExceptionText(exception), StringComparison.Ordinal);
    }

    /// <summary>US-037 OD-008: the bound itself is accepted.</summary>
    [Fact]
    public async Task RetentionYearsOnTheBound_Starts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[RetentionYearsKey] = "100";

        host.Start();

        Assert.Equal(100, host.Services.GetRequiredService<RetentionSettings>().Years);
    }

    [Fact]
    public async Task ValidRetentionYears_Starts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[RetentionYearsKey] = "5";

        host.Start();

        var settings = host.Services.GetRequiredService<RetentionSettings>();
        Assert.Equal(5, settings.Years);
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
