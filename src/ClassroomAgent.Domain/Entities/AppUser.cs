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

    /// <summary>
    /// The stored hash is of a password an Admin typed at creation or at a reset, so the next successful
    /// authentication leads to the forced change instead of a session (US-012 spec FR-006, I-2).
    /// </summary>
    public bool PasswordIsTemporary { get; private set; }

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

    /// <summary>
    /// The Dean an Admin creates by hand (BR-014; US-012 spec FR-002, FR-003). It takes a **hash**, never a
    /// password: hashing lives in <c>Infrastructure</c> behind a port, and <c>Domain</c> depends on nothing
    /// (AD-3, AD-4, US-012 OD-002). The account starts active, with the password marked temporary.
    /// </summary>
    public static AppUser CreateDean(
        string email,
        string passwordHash,
        UiLanguage uiLanguage,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        var normalized = Normalize(email);
        return new AppUser
        {
            Email = normalized,
            NormalizedEmail = normalized,
            Role = AppRole.Dean,
            SignInMethod = SignInMethod.Password,
            PasswordHash = passwordHash,
            PasswordIsTemporary = true,
            SecurityStamp = NewStamp(),
            ConcurrencyStamp = NewStamp(),
            AccessFailedCount = 0,
            LockoutEnd = null,
            UiLanguage = uiLanguage,
            IsDisabled = false,
            LastSuccessfulSignInAt = null,
        };
    }

    /// <summary>
    /// Disables the account and rotates the security stamp, so the Dean's open sessions end at their next
    /// request (US-012 spec FR-007, I-4). Password, counter, lockout and last sign-in are left untouched: the
    /// row has to stay as it was for history, audit and the retention clock (BR-014, PC-11).
    /// </summary>
    public void Disable(DateTimeOffset at)
    {
        IsDisabled = true;
        RotateSecurityStamp();
    }

    /// <summary>
    /// Clears the disabled state and **nothing else** — no temporary password, no lockout cleared
    /// (US-012 spec FR-008, BR-014).
    /// </summary>
    public void ReEnable(DateTimeOffset at)
    {
        IsDisabled = false;
        ConcurrencyStamp = NewStamp();
    }

    /// <summary>
    /// An Admin's reset: a new temporary password, the counter zeroed, the lockout cleared and the stamp
    /// rotated. A disabled account **stays disabled** (US-012 spec FR-009, S-08; BR-014 v64).
    /// </summary>
    public void ResetPassword(string passwordHash, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        // Deliberately NOT touching IsDisabled: a reset never re-enables (BR-014 v64, spec S-08).
        PasswordHash = passwordHash;
        PasswordIsTemporary = true;
        AccessFailedCount = 0;
        LockoutEnd = null;
        RotateSecurityStamp();
    }

    /// <summary>
    /// The Dean's own password, at the forced change or later: the hash is replaced, the temporary mark is
    /// cleared and the stamp is rotated (US-012 spec FR-006, FR-014, FR-019).
    /// </summary>
    public void SetOwnPassword(string passwordHash, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
        PasswordIsTemporary = false;
        RotateSecurityStamp();
    }

    /// <summary>
    /// Step 3 of the sign-in sequence: one more failed attempt and, at <paramref name="maxAttempts"/>
    /// consecutive failures, a lockout of <paramref name="lockoutFor"/> (US-012 spec FR-013, SC-2). The numbers
    /// arrive from <c>Application</c>; the entity knows no policy.
    /// </summary>
    public void RecordFailedSignIn(DateTimeOffset at, int maxAttempts, TimeSpan lockoutFor)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        AccessFailedCount++;
        if (AccessFailedCount >= maxAttempts)
        {
            LockoutEnd = at + lockoutFor;
        }

        ConcurrencyStamp = NewStamp();
    }

    /// <summary>
    /// Step 2 of the sign-in sequence: a lockout is in force, and the password is not checked at all
    /// (US-012 spec FR-012, SC-2 v66).
    /// </summary>
    public bool IsLockedOut(DateTimeOffset now) => LockoutEnd is { } end && end > now;

    /// <summary>The lower-cased form of an address, used for storage, lookup and comparison (BR-079, SC-3).</summary>
    public static string Normalize(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Records a successful sign-in (US-008 spec FR-011). Step 6 of the US-012 sequence also resets the
    /// failed-attempt counter and clears any lockout, which SC-2 requires of a success; for the Admin path,
    /// whose counter is never touched, both are no-ops (US-012 spec FR-013).
    /// </summary>
    public void RecordSuccessfulSignIn(DateTimeOffset at)
    {
        LastSuccessfulSignInAt = at;
        AccessFailedCount = 0;
        LockoutEnd = null;
        ConcurrencyStamp = NewStamp();
    }

    /// <summary>
    /// The user's own choice of UI language (US-039 spec FR-004). Not a credential change: the security stamp is
    /// not rotated (spec I-3).
    /// </summary>
    public void ChooseUiLanguage(UiLanguage language)
    {
        if (!Enum.IsDefined(language))
        {
            throw new ArgumentOutOfRangeException(nameof(language), language, null);
        }

        UiLanguage = language;
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
