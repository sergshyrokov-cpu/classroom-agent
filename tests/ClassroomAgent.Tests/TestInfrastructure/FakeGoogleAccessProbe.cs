using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using Microsoft.AspNetCore.Http;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The substituted Google port of US-011 (TC-4): scripted outcomes per scope and per read, and a record of every
/// call. No test reaches Google through it — it has no transport at all.
/// </summary>
/// <remarks>
/// Each call records whether it was made inside an HTTP request (the Admin pressed the button) or outside one (the
/// startup self-check), read from <see cref="IHttpContextAccessor"/>. The self-check runs in the background after
/// start, so without the distinction a test counting the calls of one <c>POST</c> would race it.
/// </remarks>
public sealed class FakeGoogleAccessProbe(IHttpContextAccessor httpContext) : IGoogleAccessProbe
{
    private readonly Lock _gate = new();
    private readonly List<ProbeCall> _calls = [];
    private readonly TaskCompletionSource _firstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The token the fake issues. Opaque to the program; its absence from pages and logs is asserted.</summary>
    public const string IssuedToken = "synthetic-delegated-token-7f3a";

    /// <summary>Outcome per scope; a scope not listed succeeds.</summary>
    public Dictionary<string, AccessCheckStepOutcome> Delegation { get; } = new(StringComparer.Ordinal);

    public AccessCheckStepOutcome Courses { get; set; } = AccessCheckStepOutcome.Succeeded;

    public AccessCheckStepOutcome MeetActivity { get; set; } = AccessCheckStepOutcome.Succeeded;

    /// <summary>When set, every call waits until its cancellation token fires — Google never answering (spec I-2).</summary>
    public bool NeverAnswers { get; set; }

    /// <summary>When set, every call throws — an unexpected failure inside the port.</summary>
    public bool Throws { get; set; }

    public IReadOnlyList<ProbeCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return _calls.ToList();
            }
        }
    }

    /// <summary>The calls made while serving an HTTP request.</summary>
    public IReadOnlyList<ProbeCall> RequestCalls => Calls.Where(c => c.DuringRequest).ToList();

    /// <summary>The calls made outside any request — the startup self-check.</summary>
    public IReadOnlyList<ProbeCall> BackgroundCalls => Calls.Where(c => !c.DuringRequest).ToList();

    /// <summary>Completes on the first call of any kind.</summary>
    public Task FirstCall => _firstCall.Task;

    public async Task<DelegationAttempt> RequestDelegatedTokenAsync(
        string technicalAccount,
        string scope,
        CancellationToken cancellationToken)
    {
        await RecordAsync(new ProbeCall(ProbeCallKind.Delegation, technicalAccount, scope, null, DuringRequest()), cancellationToken);
        var outcome = Delegation.GetValueOrDefault(scope, AccessCheckStepOutcome.Succeeded);
        return new DelegationAttempt(
            outcome,
            outcome == AccessCheckStepOutcome.Succeeded ? new DelegatedToken(IssuedToken) : null);
    }

    public async Task<AccessCheckStepOutcome> ReadCoursesAsync(DelegatedToken token, CancellationToken cancellationToken)
    {
        await RecordAsync(new ProbeCall(ProbeCallKind.Courses, null, null, token.Value, DuringRequest()), cancellationToken);
        return Courses;
    }

    public async Task<AccessCheckStepOutcome> ReadMeetActivityAsync(DelegatedToken token, CancellationToken cancellationToken)
    {
        await RecordAsync(new ProbeCall(ProbeCallKind.MeetActivity, null, null, token.Value, DuringRequest()), cancellationToken);
        return MeetActivity;
    }

    private bool DuringRequest() => httpContext.HttpContext is not null;

    private async Task RecordAsync(ProbeCall call, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _calls.Add(call);
        }

        _firstCall.TrySetResult();
        if (Throws)
        {
            throw new InvalidOperationException("Synthetic failure inside the Google port.");
        }

        if (NeverAnswers)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }
}
