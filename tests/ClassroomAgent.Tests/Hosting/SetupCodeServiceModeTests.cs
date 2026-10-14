using System.Text.RegularExpressions;
using ClassroomAgent.ControlPlane.Services;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassroomAgent.Tests.Hosting;

/// <summary>
/// US-047 AC-005 (spec FR-006, §7, I-3): the Control Plane built as a service with no Owner account still prints the
/// one-time setup code to standard output alone — never to its log file, the Event Log or another file.
/// </summary>
/// <remarks>
/// The real <see cref="StandardOutputOperatorConsole"/> and code generator run here, not the substitutes of the other
/// setup tests, because the destination of the code is what is under test. Standard output is captured for the one
/// hosted service that writes it; no other test writes to it (the others substitute the operator console).
/// </remarks>
public sealed partial class SetupCodeServiceModeTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AsAServiceWithoutOwner_TheSetupCodeReachesStandardOutputOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionString = await database.CreateDatabaseAsync(ct);
        try
        {
            await ControlPlaneTestHost.MigrateAsync(connectionString, ct);
            using var folders = new HostFolders();
            var logs = folders.Absolute("logs");
            var keys = folders.Absolute("keys");
            var output = new StringWriter();
            string probe;
            await using (var app = BuiltHost.Build(
                BuiltHostKind.ControlPlane, runsAsService: true, logs, keys, connectionString: connectionString))
            {
                Assert.IsType<StandardOutputOperatorConsole>(app.Services.GetRequiredService<IOperatorConsole>());
                var startup = app.Services.GetServices<IHostedService>().OfType<SetupCodeStartup>().Single();
                var standardOutput = Console.Out;
                Console.SetOut(output);
                try
                {
                    await startup.StartAsync(ct);
                }
                finally
                {
                    Console.SetOut(standardOutput);
                }

                probe = BuiltHost.WriteProbeEvent(app);
            }

            var code = SetupCode().Match(output.ToString());
            Assert.True(code.Success, "The setup code was not written to standard output.");
            var files = (await BuiltHost.ReadFilesAsync(logs, ct)).Concat(await BuiltHost.ReadFilesAsync(keys, ct)).ToList();
            Assert.Contains(files, content => content.Contains(probe, StringComparison.Ordinal));
            foreach (var secret in new[] { code.Value, code.Value.Replace("-", string.Empty, StringComparison.Ordinal) })
            {
                Assert.All(files, content => Assert.DoesNotContain(secret, content, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            await database.DropDatabaseAsync(connectionString, ct);
        }
    }

    [Fact]
    public async Task AsAService_TheControlPlaneAddsNoLoggingDestinationForTheSetupCode()
    {
        using var folders = new HostFolders();
        IReadOnlyList<string> consoleProviders;
        await using (var console = BuiltHost.Build(
            BuiltHostKind.ControlPlane, runsAsService: false, folders.Absolute("logs"), folders.Absolute("keys")))
        {
            consoleProviders = BuiltHost.LoggerProviderTypes(console);
        }

        await using var service = BuiltHost.Build(
            BuiltHostKind.ControlPlane, runsAsService: true, folders.Absolute("logs"), folders.Absolute("keys"));

        Assert.IsType<StandardOutputOperatorConsole>(service.Services.GetRequiredService<IOperatorConsole>());
        Assert.Equal(BuiltHost.SerilogLoggerFactory, BuiltHost.LoggerFactoryType(service));
        Assert.Equal(consoleProviders, BuiltHost.LoggerProviderTypes(service));
    }

    /// <summary>The code's shape: 26 Crockford Base32 characters in groups of 5, 5, 4, 4, 4, 4 (US-001 OD-005).</summary>
    [GeneratedRegex("[0-9A-HJKMNP-TV-Z]{5}-[0-9A-HJKMNP-TV-Z]{5}(-[0-9A-HJKMNP-TV-Z]{4}){4}")]
    private static partial Regex SetupCode();
}
