using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// An installation host seeded into one of the three read-only causes of BR-025, or into normal
/// operation, with the US-007 synthetic use cases registered (test strategy §3). The Control Plane is
/// never scripted to answer, so the seeded <c>legitimacy_state</c> row is exactly what the guard reads.
/// </summary>
public static class ReadOnlyModeHost
{
    /// <summary>Longer than the 7-day grace period, so the seeded success is outside it (BR-025).</summary>
    public static readonly TimeSpan BeyondGracePeriod = TimeSpan.FromDays(8);

    public enum Cause
    {
        /// <summary>No check has ever succeeded: no row at all.</summary>
        NeverConfirmed,

        /// <summary>The Owner suspended the installation.</summary>
        Suspended,

        /// <summary>More than 7 days since the last successful check.</summary>
        GracePeriodExpired,

        /// <summary>Not in read-only mode: a recent success, status active.</summary>
        NotReadOnly,
    }

    /// <summary>Every cause that puts the installation in read-only mode, for a theory.</summary>
    public static TheoryData<Cause> ReadOnlyCauses => new(Cause.NeverConfirmed, Cause.Suspended, Cause.GracePeriodExpired);

    public static async Task<InstallationTestHost> StartAsync(
        PostgreSqlFixture database,
        Cause cause,
        CancellationToken cancellationToken)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        await SeedAsync(host, cause, cancellationToken);
        host.ConfigureServices = Register;
        host.Start();
        return host;
    }

    /// <summary>The test-only use cases the enforcement is proven on.</summary>
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<SyntheticGooglePort>();
        services.AddSingleton<ISyntheticGooglePort>(p => p.GetRequiredService<SyntheticGooglePort>());
        services.AddScoped<SyntheticWriteUseCase>();
        services.AddScoped<UnguardedWriteUseCase>();
        services.AddScoped<DeclaredServiceWriteUseCase>();
        services.AddScoped<SyntheticGoogleUseCase>();
    }

    /// <summary>The seeded last successful check, or null when none ever succeeded.</summary>
    public static DateTimeOffset? LastSuccessOf(Cause cause) => cause switch
    {
        Cause.NeverConfirmed => null,
        Cause.Suspended => InstallationTestHost.DefaultStart - TimeSpan.FromHours(1),
        Cause.GracePeriodExpired => InstallationTestHost.DefaultStart - BeyondGracePeriod,
        Cause.NotReadOnly => InstallationTestHost.DefaultStart - TimeSpan.FromHours(1),
        _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null),
    };

    public static Task SeedAsync(InstallationTestHost host, Cause cause, CancellationToken cancellationToken) =>
        cause == Cause.NeverConfirmed
            ? Task.CompletedTask
            : host.InsertLegitimacyStateAsync(
                cancellationToken,
                LastSuccessOf(cause),
                status: cause == Cause.Suspended ? "suspended" : "active");

    /// <summary>Resolves a scoped service from the running host, as a use case is resolved per request.</summary>
    public static async Task InScopeAsync<T>(InstallationTestHost host, Func<T, Task> body)
        where T : notnull
    {
        using var scope = host.CreateScope();
        await body(scope.ServiceProvider.GetRequiredService<T>());
    }
}
