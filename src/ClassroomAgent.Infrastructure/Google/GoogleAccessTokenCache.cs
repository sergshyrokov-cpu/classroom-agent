using System.Collections.Concurrent;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Infrastructure.Google;

/// <summary>
/// The access tokens of one <see cref="GoogleClassroomReader"/>, in process memory only (US-077 spec FR-001…FR-005,
/// S-01, S-02). An entry holds a token string and its expiry under (technical account, scope set); it holds no key and
/// nothing built from one, and its type never shows the token through <c>ToString</c>. At most one token request per
/// pair is in flight at a time (FR-004).
/// </summary>
internal sealed class GoogleAccessTokenCache(TimeProvider timeProvider)
{
    /// <summary>
    /// How close to its expiry a token stops being used (FR-003, I-2): longer than one attempt may take. A constant,
    /// for the same reason as <c>PageSize</c>.
    /// </summary>
    private static readonly TimeSpan Margin = TimeSpan.FromMinutes(5);

    private readonly Lock _gate = new();
    private readonly Dictionary<PairKey, Entry> _entries = [];
    private readonly ConcurrentDictionary<PairKey, SemaphoreSlim> _requests = new();

    /// <summary>The key of a pair: the account exactly as passed, the scopes regardless of their order (FR-002).</summary>
    public static PairKey KeyOf(string account, IEnumerable<string> scopes) =>
        new(account, string.Join(' ', scopes.Order(StringComparer.Ordinal)));

    /// <summary>
    /// A token to send now: the remembered one while it is valid, otherwise a new one from <paramref name="request"/>,
    /// which runs alone for its pair. A read that waited re-checks, so a remembered answer serves it (FR-004). A failed
    /// request leaves nothing behind, and its failure goes to the caller (FR-005).
    /// </summary>
    public async Task<string> GetAsync(
        PairKey key,
        Func<CancellationToken, Task<IssuedToken>> request,
        CancellationToken cancellationToken)
    {
        if (ValidToken(key) is { } remembered)
        {
            return remembered;
        }

        var turn = _requests.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await turn.WaitAsync(cancellationToken);
        try
        {
            if (ValidToken(key) is { } answered)
            {
                return answered;
            }

            ForgetUnusable(key);
            var requestedAt = timeProvider.GetUtcNow();
            var issued = await request(cancellationToken);
            return Accept(key, issued, requestedAt);
        }
        finally
        {
            turn.Release();
        }
    }

    /// <summary>A refused token is dropped only if the entry still holds that token, so a newer one survives (FR-005).</summary>
    public void Forget(PairKey key, string? token)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(key, out var entry) && string.Equals(entry.Token, token, StringComparison.Ordinal))
            {
                _entries.Remove(key);
            }
        }
    }

    private string Accept(PairKey key, IssuedToken issued, DateTimeOffset requestedAt)
    {
        // VR-001, VR-002a: nothing to send, or a token Google itself reports as already expired.
        if (string.IsNullOrEmpty(issued.Token) || issued.ExpiresInSeconds <= 0)
        {
            throw new GoogleReadFailedException(GoogleReadFailureKind.Unexpected, SyncDiagnosis.Unexpected);
        }

        // VR-002: no expiry, or a life not above the margin, serves this attempt only.
        if (issued.ExpiresInSeconds is { } seconds && TimeSpan.FromSeconds(seconds) > Margin)
        {
            lock (_gate)
            {
                _entries[key] = new Entry(issued.Token, requestedAt.AddSeconds(seconds));
            }
        }

        return issued.Token;
    }

    private string? ValidToken(PairKey key)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(key, out var entry) && timeProvider.GetUtcNow() + Margin < entry.Expiry
                ? entry.Token
                : null;
        }
    }

    /// <summary>FR-005, I-4: this pair's entry is no longer valid, and every expired entry goes with it.</summary>
    private void ForgetUnusable(PairKey key)
    {
        lock (_gate)
        {
            _entries.Remove(key);
            var now = timeProvider.GetUtcNow();
            foreach (var expired in _entries.Where(e => e.Value.Expiry <= now).Select(e => e.Key).ToList())
            {
                _entries.Remove(expired);
            }
        }
    }

    /// <summary>A plain class: a record's generated members would show the token (S-01).</summary>
    private sealed class Entry(string token, DateTimeOffset expiry)
    {
        public string Token { get; } = token;

        public DateTimeOffset Expiry { get; } = expiry;
    }

    /// <summary>The pair a token belongs to (FR-002).</summary>
    internal readonly record struct PairKey(string Account, string Scopes);

    /// <summary>What the token endpoint answered. A plain class, for the reason given on <see cref="Entry"/>.</summary>
    internal sealed class IssuedToken(string? token, long? expiresInSeconds)
    {
        public string? Token { get; } = token;

        public long? ExpiresInSeconds { get; } = expiresInSeconds;
    }
}
