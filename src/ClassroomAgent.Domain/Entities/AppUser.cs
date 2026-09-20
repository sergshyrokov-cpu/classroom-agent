using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Domain.Entities;

/// <summary>
/// The account of a person inside the installation — an Admin or a Dean (US-008 entity model §2.1;
/// <c>trebovaniya.md</c> §3). One table for both roles, so the failed-attempt and lockout fields exist from
/// this Story although the Admin path never uses them (spec I-3).
/// </summary>
/// <remarks>
/// Construction is through a factory, never a public constructor: <see cref="CreateAdmin"/> takes no password
/// parameter at all, so an Admin with a password hash is impossible at the type level as well as in the
/// schema (S-04, BR-010). No Identity, EF Core or ASP.NET type appears here — <c>Domain</c> depends on
/// nothing (AD-3, spec I-2).
/// </remarks>
public sealed class AppUser
{
    private AppUser()
    {
        Email = string.Empty;
        NormalizedEmail = string.Empty;
        SecurityStamp = string.Empty;
        ConcurrencyStamp = string.Empty;
    }

    public long Id { get; private set; }

    /// <summary>The account identifier, stored lower-cased (BR-079). There is no separate login field (§3 v55).</summary>
    public string Email { get; private set; }

    /// <summary>The form used for lookup and uniqueness.</summary>
    public string NormalizedEmail { get; private set; }

    public AppRole Role { get; private set; }

    public SignInMethod SignInMethod { get; private set; }

    /// <summary>Null for every Admin — not empty, not random (S-04, BR-010, SC-2).</summary>
    public string? PasswordHash { get; private set; }

    public string SecurityStamp { get; private set; }

    public string ConcurrencyStamp { get; private set; }

    /// <summary>Sign-in bookkeeping; unused by the Admin path (spec I-3).</summary>
    public int AccessFailedCount { get; private set; }

    /// <summary>Sign-in bookkeeping; unused by the Admin path (spec I-3).</summary>
    public DateTimeOffset? LockoutEnd { get; private set; }

    /// <summary>The school default at creation; only US-039 lets the user change it.</summary>
    public UiLanguage UiLanguage { get; private set; }

    /// <summary>False at creation; only US-012 sets it, and only for a Dean (spec I-9).</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>Null until the first success. PC-11 counts the retention period from it, or from creation.</summary>
    public DateTimeOffset? LastSuccessfulSignInAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// The Admin created just-in-time at their first successful sign-in (BR-011; spec FR-011). It takes no
    /// password parameter: role Admin always signs in through Google and never has a hash.
    /// </summary>
    public static AppUser CreateAdmin(string email, UiLanguage uiLanguage, DateTimeOffset signedInAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var normalized = Normalize(email);
        return new AppUser
        {
            Email = normalized,
            NormalizedEmail = normalized,
            Role = AppRole.Admin,
            SignInMethod = SignInMethod.Google,
            PasswordHash = null,
            SecurityStamp = NewStamp(),
            ConcurrencyStamp = NewStamp(),
            AccessFailedCount = 0,
            LockoutEnd = null,
            UiLanguage = uiLanguage,
            IsDisabled = false,
            LastSuccessfulSignInAt = signedInAt,
        };
    }

    /// <summary>The lower-cased form of an address, used for storage, lookup and comparison (BR-079, SC-3).</summary>
    public static string Normalize(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>Records a successful sign-in (spec FR-011).</summary>
    public void RecordSuccessfulSignIn(DateTimeOffset at)
    {
        LastSuccessfulSignInAt = at;
        ConcurrencyStamp = NewStamp();
    }

    /// <summary>
    /// Ends every session issued so far: the stamp travels in the session cookie and is compared with this one on
    /// every request, so rotating it stops a cookie someone kept a copy of (US-008 AC-014, spec FR-016).
    /// </summary>
    public void RotateSecurityStamp()
    {
        SecurityStamp = NewStamp();
        ConcurrencyStamp = NewStamp();
    }

    private static string NewStamp() => Guid.NewGuid().ToString("N");
}
