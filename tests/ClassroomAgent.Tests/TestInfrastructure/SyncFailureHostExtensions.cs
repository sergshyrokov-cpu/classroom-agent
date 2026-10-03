using ClassroomAgent.Application.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Starts the synchronization host of <see cref="SyncHostExtensions"/> with the Classroom port replaced by a
/// <see cref="FailingClassroomReader"/> that wraps the host's own fake (US-017, TC-4).
/// </summary>
public static class SyncFailureHostExtensions
{
    public static Task<InstallationTestHost> StartFailingAsync(
        PostgreSqlFixture database,
        CancellationToken cancellationToken,
        Action<FakeClassroomReader>? classroom = null,
        Action<FailingClassroomReader>? failures = null,
        int? intervalMinutes = null) =>
        SyncHostExtensions.StartAsync(
            database,
            cancellationToken,
            intervalMinutes: intervalMinutes,
            classroom: classroom,
            seed: (host, _) =>
            {
                var failing = new FailingClassroomReader(host.Classroom);
                failures?.Invoke(failing);
                var previous = host.ConfigureServices;
                host.ConfigureServices = services =>
                {
                    previous?.Invoke(services);
                    services.RemoveAll<IClassroomReader>();
                    services.AddSingleton<IClassroomReader>(failing);
                };
                return Task.CompletedTask;
            });
}
