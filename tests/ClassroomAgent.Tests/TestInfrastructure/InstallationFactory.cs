using ClassroomAgent.Application.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The installation host in process, with the seams of the US-005 test strategy 3 replaced: the clock
/// and the Control Plane client (TC-4 — no test calls a real Control Plane from the installation).
/// </summary>
/// <remarks>
/// US-008 adds a second Control Plane seam. When <paramref name="controlPlaneHandler"/> is given the
/// substituted client is <b>not</b> registered: the installation's real <c>ControlPlaneClient</c> runs and
/// only the transport underneath it is scripted, so the classification of an HTTP outcome into a typed
/// reply (api-design 2.4) is proven on production code (test strategy 2.2).
/// </remarks>
public sealed class InstallationFactory(
    IReadOnlyDictionary<string, string?> settings,
    TimeProvider timeProvider,
    IControlPlaneClient controlPlaneClient,
    FakeClassroomReader classroomReader,
    FakeMeetReportsReader meetReader,
    Action<IServiceCollection>? configureServices = null,
    HttpMessageHandler? controlPlaneHandler = null) : WebApplicationFactory<ClassroomAgent.Web.Program>
{
    public const string EnvironmentName = "Test";

    /// <summary>The logical name of the typed <c>IControlPlaneClient</c> HttpClient (US-005 <c>InstallationServices</c>).</summary>
    public const string ControlPlaneClientName = nameof(IControlPlaneClient);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(timeProvider);

            if (controlPlaneHandler is null)
            {
                services.RemoveAll<IControlPlaneClient>();
                services.AddSingleton(controlPlaneClient);
            }
            else
            {
                // The real typed client stays; only its transport is scripted. Registered last, so this
                // primary handler is the one the factory builds (US-008 test strategy 2.2).
                services.AddHttpClient(ControlPlaneClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => controlPlaneHandler);
            }

            // US-014, TC-4: the Classroom port is substituted in every host test. Once the import step exists a
            // run would otherwise try to reach a live Google API from the host's own composition root.
            services.RemoveAll<IClassroomReader>();
            services.AddSingleton(classroomReader);
            services.AddSingleton<IClassroomReader>(p => p.GetRequiredService<FakeClassroomReader>());

            // US-031 spec FR-015, TC-4, AC-015: the Meet port too — no host test resolves the real adapter and calls it.
            services.RemoveAll<IMeetReportsReader>();
            services.AddSingleton<IMeetReportsReader>(meetReader);

            // US-007: the synthetic write and Google use cases the enforcement is proven on (test strategy 3).
            configureServices?.Invoke(services);
        });
    }
}
