using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>
/// At startup, if and only if no Owner account exists, generates the one-time setup code
/// and prints it to the operator console — never to the log (FR-002, SC-10).
/// </summary>
public sealed class SetupCodeStartup(
    IServiceScopeFactory scopeFactory,
    ISetupCodeGenerator generator,
    IOperatorConsole operatorConsole,
    SetupCodeState state) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<OwnerSessionService>();
        if (await sessions.OwnerExistsAsync(cancellationToken))
        {
            return;
        }

        var code = generator.Generate();
        state.Set(code);
        operatorConsole.WriteLine($"Control Plane first-run setup code: {code}");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
