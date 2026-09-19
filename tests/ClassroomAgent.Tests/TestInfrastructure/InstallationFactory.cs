using ClassroomAgent.Application.Ports;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The installation host in process, with the seams of the US-005 test strategy §3 replaced: the clock
/// and the Control Plane client (TC-4 — no test calls a real Control Plane from the installation).
/// </summary>
public sealed class InstallationFactory(
    IReadOnlyDictionary<string, string?> settings,
    TimeProvider timeProvider,
    IControlPlaneClient controlPlaneClient,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<ClassroomAgent.Web.Program>
{
    public const string EnvironmentName = "Test";

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
            services.RemoveAll<IControlPlaneClient>();
            services.AddSingleton(controlPlaneClient);

            // US-007: the synthetic write and Google use cases the enforcement is proven on (test strategy §3).
            configureServices?.Invoke(services);
        });
    }
}
