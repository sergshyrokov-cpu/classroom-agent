using Microsoft.Extensions.Hosting.WindowsServices;

namespace ClassroomAgent.Web.Configuration;

/// <summary>
/// US-047: the installation host as a Windows service (spec FR-002, FR-003, FR-007). Outside a service the builder is
/// exactly <c>WebApplication.CreateBuilder(args)</c> (AC-004).
/// </summary>
public static class WindowsServiceHosting
{
    /// <summary>
    /// As a service, the content root is the application folder — a service starts in the Windows system folder — and
    /// the host runs with the Windows service lifetime, so the Service Control Manager sees "running" and "stopped".
    /// </summary>
    /// <remarks>
    /// The lifetime is registered here rather than through the package's <c>AddWindowsService</c>: that helper
    /// re-checks the detection itself, and it also registers a Windows Event Log logging provider, which the
    /// application must not have (FR-007, I-4).
    /// </remarks>
    public static WebApplicationBuilder CreateBuilder(string[] args, bool runsAsService)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = runsAsService ? AppContext.BaseDirectory : null,
        });
        if (runsAsService && OperatingSystem.IsWindows())
        {
            builder.Services.AddSingleton<IHostLifetime, WindowsServiceLifetime>();
        }

        return builder;
    }
}
