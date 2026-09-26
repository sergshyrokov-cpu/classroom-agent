using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Web.Security;

namespace ClassroomAgent.Web.BackgroundServices;

/// <summary>
/// The startup access self-check (US-011 spec FR-010; DC-5, DC-10): once per start, after the host has started, in
/// the background. It never delays or stops the start; its outcome goes to the log and nowhere else — no audit row,
/// no table, no screen.
/// </summary>
public sealed class StartupSelfCheckBackgroundService(
    IServiceScopeFactory scopes,
    ILogger<StartupSelfCheckBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the host finish starting first: the check must never hold the start up (spec FR-010).
        await Task.Yield();
        try
        {
            using var scope = scopes.CreateScope();
            var selfCheck = scope.ServiceProvider.GetRequiredService<RunStartupSelfCheckUseCase>();
            var outcome = await selfCheck.ExecuteAsync(stoppingToken);
            if (outcome.Result is { } result)
            {
                AccessCheckLog.SelfCheckCompleted(logger, result);
            }
            else if (outcome.ReadOnlyReason is { } reason)
            {
                AccessCheckLog.SelfCheckSkipped(logger, "ReadOnly:" + reason);
            }
            else
            {
                AccessCheckLog.SelfCheckSkipped(logger, "Connection:" + outcome.ConnectionState);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown before the check finished.
        }
        catch (Exception failure)
        {
            // A failing self-check is logged and never takes the installation down (spec FR-010).
            AccessCheckLog.SelfCheckFailed(logger, failure);
        }
    }
}
