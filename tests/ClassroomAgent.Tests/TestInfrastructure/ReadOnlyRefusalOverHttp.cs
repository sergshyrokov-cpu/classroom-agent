using System.Globalization;
using System.Text.Json;
using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Puts a <see cref="ReadOnlyModeException"/> through the host's own exception handler and reports what a
/// caller would receive (US-008 spec FR-014; API-5, API-6). This closes US-007 OD-001.
/// </summary>
/// <remarks>
/// api-design 2.5 suggested proving the mapping with a test-only probe endpoint in the test host. That is not
/// available from outside the host: FR-002 puts an anonymous catch-all last in the endpoint pipeline, so it
/// matches every unmatched path and an appended test middleware would never run, while a middleware inserted
/// ahead of the app's pipeline would sit outside the exception handler being tested. Invoking the registered
/// <see cref="IExceptionHandler"/> directly tests the same production code with nothing simulated, so that is
/// what these tests do; the deviation is recorded in the test-generation report.
/// </remarks>
public static class ReadOnlyRefusalOverHttp
{
    /// <summary>Hands the refusal to the host's handler for that request path and culture.</summary>
    public static async Task<Refusal> HandleAsync(
        InstallationTestHost host,
        string path,
        LegitimacyModeReason reason,
        DateTimeOffset? lastSuccessfulCheckAt,
        string culture,
        CancellationToken cancellationToken)
    {
        var handlers = host.Services.GetServices<IExceptionHandler>().ToList();
        Assert.True(
            handlers.Count > 0,
            "The host registers no IExceptionHandler, so a read-only refusal reaching HTTP is unmapped (spec FR-014).");

        using var scope = host.CreateScope();
        var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("school-one.example.test");
        context.Response.Body = body;

        var previous = CultureInfo.CurrentUICulture;
        var handled = false;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var exception = new ReadOnlyModeException(reason, lastSuccessfulCheckAt, "test.probe");
            foreach (var handler in handlers)
            {
                handled = await handler.TryHandleAsync(context, exception, cancellationToken);
                if (handled)
                {
                    break;
                }
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }

        body.Position = 0;
        using var reader = new StreamReader(body);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return new Refusal(handled, context.Response.StatusCode, context.Response.ContentType, text);
    }

    /// <summary>What the caller received.</summary>
    public sealed record Refusal(bool Handled, int StatusCode, string? ContentType, string Body)
    {
        /// <summary>The body as the API-6 error object; fails when it is not one.</summary>
        public IReadOnlyDictionary<string, JsonElement> ApiError()
        {
            Assert.False(string.IsNullOrWhiteSpace(Body), "The refusal carried no body.");
            using var document = JsonDocument.Parse(Body);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
            return document.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
        }
    }
}
