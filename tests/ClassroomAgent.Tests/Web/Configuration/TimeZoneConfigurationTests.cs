using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.Configuration;

/// <summary>
/// US-025 spec FR-010, AC-015: the required <c>Installation:TimeZone</c> setting — an IANA id. Missing, blank,
/// unknown or a Windows id stop the host's start and the message names the key; a valid id (padding trimmed) lets
/// it start. Mirrors <see cref="RetentionConfigurationTests"/>.
/// </summary>
public sealed class TimeZoneConfigurationTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task MissingTimeZone_RefusesToStart()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings.Remove(JournalTestData.TimeZoneKey);

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(JournalTestData.TimeZoneKey, ExceptionText(exception), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("Not/AZone")]
    [InlineData("FLE Standard Time")]
    public async Task InvalidTimeZone_RefusesToStart(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[JournalTestData.TimeZoneKey] = value;

        var exception = Assert.ThrowsAny<Exception>(host.Start);

        Assert.Contains(JournalTestData.TimeZoneKey, ExceptionText(exception), StringComparison.Ordinal);
    }

    /// <summary>The Kyiv IANA id this runtime knows (<see cref="JournalTestData.KyivZoneId"/>), plain and padded (VR-006: trimmed).</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidTimeZone_Starts(bool padded)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[JournalTestData.TimeZoneKey] = padded ? $" {JournalTestData.KyivZoneId} " : JournalTestData.KyivZoneId;

        host.Start();
    }

    /// <summary>OD-010 (a): both spellings of the Kyiv zone start the host, whichever one this runtime knows.</summary>
    [Theory]
    [InlineData("Europe/Kyiv")]
    [InlineData("Europe/Kiev")]
    public async Task EitherKyivSpelling_Starts(string value)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await InstallationTestHost.CreateAsync(database, ct);
        host.Settings[JournalTestData.TimeZoneKey] = value;

        host.Start();
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
