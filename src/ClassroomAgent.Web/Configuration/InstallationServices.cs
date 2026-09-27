using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Contracts;
using ClassroomAgent.Infrastructure.ControlPlane;
using ClassroomAgent.Infrastructure.Google;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Infrastructure.Security;
using ClassroomAgent.Infrastructure.Persistence.Repositories;
using ClassroomAgent.Infrastructure.ReadOnly;
using ClassroomAgent.Infrastructure.Secrets;
using ClassroomAgent.Web.BackgroundServices;
using ClassroomAgent.Web.Security;
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

        // US-013 spec FR-004, FR-013, FR-014, FR-018: the run interval reaches the service as configuration, the
        // coordinator keeps one run at a time, and the marker is what readiness reads (DC-11).
        services.AddSingleton(new SyncScheduleSettings(settings.SyncInterval));
        services.AddSingleton<SyncRunCoordinator>();
        services.AddSingleton<SynchronizationServiceMemory>();

        services.AddSingleton<TimestampInterceptor>();
        services.AddDbContext<ClassroomAgentDbContext>((provider, options) =>
        {
            ClassroomAgentDbContextOptions.Configure(options, settings.ConnectionString);
            options.AddInterceptors(provider.GetRequiredService<TimestampInterceptor>());
        });
        services.AddScoped<ILegitimacyStateRepository, LegitimacyStateRepository>();
        services.AddScoped<IAppUserRepository, AppUserRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IWorkspaceConnectionRepository, WorkspaceConnectionRepository>();

        // US-008 spec FR-011, FR-017: the school-wide defaults a new account inherits.
        services.AddSingleton(new SchoolDefaults(settings.DefaultUiLanguage));

        // US-007 FR-011: the enforcement point and the commit backstop, each behind the decorator that
        // logs a refusal (FR-009). Scoped, never singleton: a singleton would outlive the DbContext it
        // reads through and invite the cached mode AC-008 forbids.
        services.AddScoped<ServiceWriteScope>();

        // US-008 FR-021 corrects US-007 security-review finding F-1: both are constructed inside the decorator
        // factories and neither is registered bare, so no type in Web can inject the undecorated unit of work and
        // commit past the read-only backstop (S-20, SC-5).
        services.AddScoped<IReadOnlyModeGuard>(provider => new LoggingReadOnlyModeGuard(
            new ReadOnlyModeGuard(
                provider.GetRequiredService<GetLegitimacyModeQuery>()),
            provider.GetRequiredService<ILogger<LoggingReadOnlyModeGuard>>()));
        services.AddScoped<IUnitOfWork>(provider => new LoggingUnitOfWork(
            new ReadOnlyModeUnitOfWork(
                new UnitOfWork(provider.GetRequiredService<ClassroomAgentDbContext>()),
                provider.GetRequiredService<GetLegitimacyModeQuery>(),
                provider.GetRequiredService<ServiceWriteScope>()),
            provider.GetRequiredService<ILogger<LoggingUnitOfWork>>()));

        // The only outbound destination: the configured Control Plane address (SC-13). Redirects are not
        // followed and no cookie is kept (api-design §6); the certificate is validated by the platform (S-05).
        services.AddHttpClient<IControlPlaneClient, ControlPlaneClient>(client => client.BaseAddress = BaseAddress(settings.ControlPlaneAddress))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });

        services.AddScoped<CheckLegitimacyUseCase>();
        services.AddScoped<CompleteGoogleSignInUseCase>();
        services.AddScoped<AccountSessionService>();
        services.AddScoped<GetLegitimacyModeQuery>();
        services.AddScoped<GetReadinessQuery>();

        // US-009 spec FR-015: the connection query and the guarded save of the settings screen.
        services.AddScoped<GetWorkspaceConnectionQuery>();
        services.AddScoped<SaveWorkspaceConnectionUseCase>();

        // US-010 spec FR-016: the instruction query. It reads and writes nothing, so it needs no guard and no
        // unit of work; the scope list is a constant in Domain and needs no registration (spec FR-004, I-2).
        services.AddScoped<GetConnectionInstructionQuery>();

        // US-011 spec FR-004, FR-015, FR-016: the first real Google port. Its transport reaches Google only
        // (SC-13), follows no redirect and keeps no cookie; the key is resolved per request from the store the
        // reference names. Both use cases take the read-only guard (US-007 FR-007).
        services.AddSingleton(new GoogleServiceAccountSettings(settings.ServiceAccountKeyReference));
        services.AddSingleton<IGoogleAccessProbe>(provider => new GoogleAccessProbe(
            provider.GetRequiredService<ISecretStore>(),
            provider.GetRequiredService<GoogleServiceAccountSettings>(),
            new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false },
            provider.GetRequiredService<ILogger<GoogleAccessProbe>>()));
        services.AddScoped<RunAccessCheckUseCase>();

        // US-012: the Dean accounts screen, the Dean's sign-in and the two password changes (spec FR-003,
        // FR-007 … FR-014). The password hasher is Identity's, behind the Application port (OD-002, FR-018).
        services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<AppUser>,
            Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>>();
        services.AddScoped<IPasswordHasher, PasswordHasherAdapter>();
        services.AddScoped<ListDeanAccountsQuery>();
        services.AddScoped<CreateDeanAccountUseCase>();
        services.AddScoped<SetDeanAccountStateUseCase>();
        services.AddScoped<ResetDeanPasswordUseCase>();
        services.AddScoped<SignInDeanUseCase>();
        services.AddScoped<CompleteTemporaryPasswordChangeUseCase>();
        services.AddScoped<ChangeOwnPasswordUseCase>();
        services.AddScoped<RunStartupSelfCheckUseCase>();

        // US-013 spec FR-005, FR-006, FR-015: the run and the single sync_state row.
        services.AddScoped<ISyncStateRepository, SyncStateRepository>();
        services.AddScoped<RunSynchronizationUseCase>();
        services.AddHostedService<StartupSelfCheckBackgroundService>();
        services.AddHostedService<LegitimacyCheckBackgroundService>();
        services.AddHostedService<SynchronizationBackgroundService>();
        return services;
    }

    /// <summary>With a trailing slash, so the check path resolves below any path of the address.</summary>
    private static Uri BaseAddress(Uri address) =>
        address.AbsolutePath.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
}
