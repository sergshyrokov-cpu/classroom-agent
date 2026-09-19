using System.Text.Json;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Contracts;
using ClassroomAgent.Web.BackgroundServices;

namespace ClassroomAgent.Web.Security;

/// <summary>
/// The status-change push receiver (US-006 spec FR-009; api-design §4). It validates the body, compares the
/// installation id with the configured one and asks the check coordinator — it writes nothing itself and
/// answers <c>202</c> without waiting for the check. The body is never logged or echoed (SC-10), and the
/// endpoint behaves identically in read-only mode (spec FR-012).
/// </summary>
public static partial class StatusPushEndpoint
{
    /// <summary>At most 4 KB: the payload is one UUID (spec I-12).</summary>
    public const int MaximumBodyBytes = 4096;

    private const string TooLarge = "too_large";

    private const string Invalid = "invalid";

    public static async Task<IResult> ReceiveAsync(
        HttpContext context,
        InstallationIdentity identity,
        PushCheckCoordinator pushChecks,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var logger = loggers.CreateLogger(typeof(StatusPushEndpoint));
        if (context.Request.ContentLength > MaximumBodyBytes)
        {
            LogRejected(logger, TooLarge);
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var body = await ReadAsync(context.Request, cancellationToken);
        if (body is null || !IsJson(context.Request.ContentType))
        {
            LogRejected(logger, Invalid);
            return InvalidRequest();
        }

        if (body.Length > MaximumBodyBytes)
        {
            LogRejected(logger, TooLarge);
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        StatusPushRequest? push;
        try
        {
            push = JsonSerializer.Deserialize<StatusPushRequest>(body, ServiceChannel.JsonOptions);
        }
        catch (JsonException)
        {
            // The message would quote the body (SC-10); only the category is logged.
            push = null;
        }

        if (push?.InstallationId is not { } installationId)
        {
            LogRejected(logger, Invalid);
            return InvalidRequest();
        }

        if (installationId != identity.InstallationId)
        {
            // The received id is not logged either: it is not needed (spec I-10).
            LogForeignInstallation(logger);
            return Results.Json(new ServiceOutcome(ServiceOutcome.UnknownInstallation), ServiceChannel.JsonOptions, statusCode: StatusCodes.Status404NotFound);
        }

        if (pushChecks.Request() == PushCheckRequestResult.Started)
        {
            LogAccepted(logger);
        }

        // A deferred push is not logged; the pending check logs itself when it starts (spec FR-011).
        return Results.StatusCode(StatusCodes.Status202Accepted);
    }

    private static IResult InvalidRequest() =>
        Results.Json(new ServiceOutcome(ServiceOutcome.InvalidRequest), ServiceChannel.JsonOptions, statusCode: StatusCodes.Status400BadRequest);

    private static bool IsJson(string? contentType) =>
        contentType is not null && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads at most one byte beyond the limit, so an oversized body is recognised without buffering it.</summary>
    private static async Task<string?> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaximumBodyBytes + 1];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await request.Body.ReadAsync(buffer.AsMemory(read), cancellationToken);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        return read == 0 ? null : System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }

    [LoggerMessage(EventId = 5111, EventName = "StatusPushAccepted", Level = LogLevel.Information, Message = "Status push accepted; a legitimacy check started")]
    private static partial void LogAccepted(ILogger logger);

    [LoggerMessage(EventId = 5112, EventName = "StatusPushForeignInstallation", Level = LogLevel.Warning, Message = "A status push for another installation arrived and was refused")]
    private static partial void LogForeignInstallation(ILogger logger);

    [LoggerMessage(EventId = 5113, EventName = "StatusPushRejected", Level = LogLevel.Error, Message = "A status push was rejected: {Category}")]
    private static partial void LogRejected(ILogger logger, string category);
}
