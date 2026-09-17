using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The private route group (US-005 spec FR-013; api-design §7; DC-6, DC-11): liveness and readiness, GET only,
/// anonymous as the SC-4 "Liveness and readiness" entry, plain text with the state word only (S-09).
/// </summary>
public static class PrivateEndpoints
{
    private const string TextPlain = "text/plain";

    public static RouteGroupBuilder MapPrivateEndpoints(this IEndpointRouteBuilder endpoints, int privatePort)
    {
        var group = endpoints.MapGroup("/health")
            .AllowAnonymous()
            .AddEndpointFilter(new PrivatePortEndpointFilter(privatePort));

        // The process runs; no dependency is touched.
        group.MapGet("/live", () => Results.Text(nameof(ReadinessState.Healthy), TextPlain));

        group.MapGet("/ready", async ([FromServices] GetReadinessQuery readiness, CancellationToken cancellationToken) =>
        {
            var state = await readiness.ExecuteAsync(cancellationToken);
            return Results.Text(
                state.ToString(),
                TextPlain,
                statusCode: state == ReadinessState.Unhealthy ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status200OK);
        });

        return group;
    }
}
