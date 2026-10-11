using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-045 spec I-6: no production code writes a line for every request at today's levels, so a test host registers
/// probe lines of its own — at the very start of the pipeline (before routing, I-1), after the whole pipeline has run,
/// and inside every MVC endpoint (a page, the error page). The log configuration under test stays the production one;
/// the assertions are on the file.
/// </summary>
internal static class RouteProbe
{
    /// <summary>US-045 spec FR-003: the fixed marker of a line written while no page serves the request.</summary>
    public const string Marker = "(unmatched)";

    public const string BeforeRouting = "RouteProbeBeforeRouting";

    public const string AfterPipeline = "RouteProbeAfterPipeline";

    public const string InEndpoint = "RouteProbeInEndpoint";

    public const string RequestPath = "RequestPath";

    public const string RequestId = "RequestId";

    private const string Category = "ClassroomAgent.Tests.RouteProbe";

    private static readonly EventId BeforeRoutingEvent = new(45001, BeforeRouting);

    private static readonly EventId AfterPipelineEvent = new(45002, AfterPipeline);

    private static readonly EventId InEndpointEvent = new(45003, InEndpoint);

    /// <summary>Adds the three probe lines to a host's services.</summary>
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<IStartupFilter, PipelineProbe>();
        services.Configure<MvcOptions>(options => options.Filters.Add(new EndpointProbe()));
    }

    /// <summary>The number of events written so far; a torn last line is retried.</summary>
    public static async Task<int> BaselineAsync(string logDirectory, CancellationToken cancellationToken) =>
        (await ReadAsync(logDirectory, cancellationToken)).Count;

    /// <summary>
    /// Waits (real time, bounded) until the events written after <paramref name="baseline"/> hold at least
    /// <paramref name="count"/> probe lines named <paramref name="eventName"/>; returns the events after the baseline.
    /// </summary>
    public static async Task<IReadOnlyList<LogEvent>> WaitAsync(
        string logDirectory,
        int baseline,
        string eventName,
        int count,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ManualTimeProvider.RealTimeLimit;
        while (true)
        {
            var events = (await ReadAsync(logDirectory, cancellationToken)).Skip(baseline).ToList();
            var seen = events.Count(e => e.EventName == eventName);
            if (seen >= count)
            {
                return events;
            }

            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail($"Timed out waiting for {count} '{eventName}' probe line(s); {seen} written.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }

    /// <summary>The <c>RequestPath</c> of each probe line named <paramref name="eventName"/>, in file order.</summary>
    public static IReadOnlyList<string?> PathsOf(IEnumerable<LogEvent> events, string eventName) =>
        events.Where(e => e.EventName == eventName).Select(e => e.Property(RequestPath)).ToList();

    /// <summary>
    /// US-045 spec §6, FR-004, FR-005, I-4: every line written inside a request carries a non-empty request id and a
    /// <c>RequestPath</c> that is either the marker or a route template (leading <c>/</c>), and no line of the file —
    /// inside a request or not — holds <paramref name="value"/>.
    /// </summary>
    public static async Task AssertTheRuleAsync(string logDirectory, string value, CancellationToken cancellationToken)
    {
        var files = await HostLogs.ReadFilesAsync(logDirectory, cancellationToken);
        foreach (var file in files)
        {
            Assert.DoesNotContain(value, file, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var e in LogEvent.Parse(files).Where(e => e.EventName is BeforeRouting or AfterPipeline or InEndpoint))
        {
            Assert.False(string.IsNullOrEmpty(e.Property(RequestId)), $"A probe line has no request id: {e.Line}");
        }

        foreach (var e in LogEvent.Parse(files).Where(e => e.Json.TryGetProperty(RequestId, out _)))
        {
            var path = e.Property(RequestPath);
            Assert.False(string.IsNullOrEmpty(path), $"A line written inside a request has no RequestPath: {e.Line}");
            Assert.True(
                path == Marker || (path!.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal)),
                $"RequestPath is neither the marker nor a route template: {path}");
        }
    }

    private static async Task<IReadOnlyList<LogEvent>> ReadAsync(string logDirectory, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + ManualTimeProvider.RealTimeLimit;
        while (true)
        {
            try
            {
                return await HostLogs.ReadEventsAsync(logDirectory, cancellationToken);
            }
            catch (JsonException) when (DateTime.UtcNow < deadline)
            {
                // The host may be midway through writing a line.
                await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
            }
        }
    }

    /// <summary>A line before anything else in the pipeline runs, and one after all of it has run.</summary>
    private sealed class PipelineProbe : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                var logger = app.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger(Category);
                app.Use(async (context, nextMiddleware) =>
                {
                    logger.LogWarning(BeforeRoutingEvent, "Route probe before routing");
                    await nextMiddleware(context);
                    logger.LogWarning(AfterPipelineEvent, "Route probe after the pipeline");
                });
                next(app);
            };
    }

    /// <summary>A line while an MVC endpoint — a page or the error page — executes.</summary>
    private sealed class EndpointProbe : IAsyncResourceFilter
    {
        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
        {
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(Category)
                .LogWarning(InEndpointEvent, "Route probe in an endpoint");
            await next();
        }
    }
}
