using ClassroomAgent.ControlPlane.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The Control Plane host in process: its own database, key and log directories, and
/// the seams of the test strategy §3 replaced.
/// </summary>
public sealed class ControlPlaneFactory(
    IReadOnlyDictionary<string, string> settings,
    TimeProvider timeProvider,
    ISetupCodeGenerator setupCodeGenerator,
    IOperatorConsole operatorConsole) : WebApplicationFactory<Program>
{
    /// <summary>Not <c>Development</c>: the developer exception page must not be in play (S-17).</summary>
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
            services.RemoveAll<ISetupCodeGenerator>();
            services.AddSingleton(setupCodeGenerator);
            services.RemoveAll<IOperatorConsole>();
            services.AddSingleton(operatorConsole);
        });
    }
}
