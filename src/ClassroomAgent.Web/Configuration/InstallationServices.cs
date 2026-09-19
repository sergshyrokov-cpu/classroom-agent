using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Contracts;
using ClassroomAgent.Infrastructure.ControlPlane;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Infrastructure.Persistence.Repositories;
using ClassroomAgent.Infrastructure.ReadOnly;
using ClassroomAgent.Web.BackgroundServices;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClassroomAgent.Web.Configuration;

/// <summary>DI wiring of the installation (US-005 spec FR-014): ports to their Infrastructure implementations, use cases, the check schedule.</summary>
public static class InstallationServices
{
    public static IServiceCollection AddInstallation(this IServiceCollection services, InstallationSettings settings)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new InstallationIdentity(
            settings.InstallationId,
            ReleaseVersion.Of(typeof(InstallationServices).Assembly),
            ContractVersion.Current));
        services.AddSingleton<LegitimacyCheckMemory>();
        services.AddSingleton<PushCheckCoordinator>();

        services.AddSingleton<TimestampInterceptor>();
        services.AddDbContext<ClassroomAgentDbContext>((provider, options) =>
        {
            ClassroomAgentDbContextOptions.Configure(options, settings.ConnectionString);
            options.AddInterceptors(provider.GetRequiredService<TimestampInterceptor>());
        });
        services.AddScoped<ILegitimacyStateRepository, LegitimacyStateRepository>();

        // US-007 FR-011: the enforcement point and the commit backstop, each behind the decorator that
        // logs a refusal (FR-009). Scoped, never singleton: a singleton would outlive the DbContext it
        // reads through and invite the cached mode AC-008 forbids.
        services.AddScoped<ServiceWriteScope>();
        services.AddScoped<ReadOnlyModeGuard>();
        services.AddScoped<IReadOnlyModeGuard>(provider => new LoggingReadOnlyModeGuard(
            provider.GetRequiredService<ReadOnlyModeGuard>(),
            provider.GetRequiredService<ILogger<LoggingReadOnlyModeGuard>>()));
        services.AddScoped<UnitOfWork>();
        services.AddScoped<IUnitOfWork>(provider => new LoggingUnitOfWork(
            new ReadOnlyModeUnitOfWork(
                provider.GetRequiredService<UnitOfWork>(),
                provider.GetRequiredService<GetLegitimacyModeQuery>(),
                provider.GetRequiredService<ServiceWriteScope>()),
            provider.GetRequiredService<ILogger<LoggingUnitOfWork>>()));

        // The only outbound destination: the configured Control Plane address (SC-13). Redirects are not
        // followed and no cookie is kept (api-design §6); the certificate is validated by the platform (S-05).
        services.AddHttpClient<IControlPlaneClient, ControlPlaneClient>(client => client.BaseAddress = BaseAddress(settings.ControlPlaneAddress))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });

        services.AddScoped<CheckLegitimacyUseCase>();
        services.AddScoped<GetLegitimacyModeQuery>();
        services.AddScoped<GetReadinessQuery>();
        services.AddHostedService<LegitimacyCheckBackgroundService>();
        return services;
    }

    /// <summary>With a trailing slash, so the check path resolves below any path of the address.</summary>
    private static Uri BaseAddress(Uri address) =>
        address.AbsolutePath.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
}
