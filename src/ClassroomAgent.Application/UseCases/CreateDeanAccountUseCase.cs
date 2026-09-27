using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.Validation;
using ClassroomAgent.Domain.Entities;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// An Admin creates a Dean account (US-012 spec FR-003). The evaluation order is fixed: the read-only guard
/// first, before any repository call, then the address, the school's domain, the password policy and finally
/// uniqueness. On success the account and its audit row commit inside one transaction.
/// </summary>
public sealed class CreateDeanAccountUseCase(
    IReadOnlyModeGuard readOnlyMode,
    IAppUserRepository users,
    ILegitimacyStateRepository legitimacyStates,
    IAuditEventRepository auditEvents,
    IPasswordHasher passwordHasher,
    SchoolDefaults schoolDefaults,
    IUnitOfWork unitOfWork,
    ServiceWriteScope writeScope,
    TimeProvider timeProvider)
{
    /// <summary>The operation name the read-only guard records; a constant, never user data (SC-10).</summary>
    public const string Operation = "DeanAccount.Create";

    public async Task<DeanAccountActionOutcome> ExecuteAsync(
        long adminId,
        string email,
        string temporaryPassword,
        string? requestId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(temporaryPassword);

        // 1: read-only mode, before any repository call, any hash and any transaction (AD-6, spec FR-015).
        try
        {
            await readOnlyMode.EnsureAllowedAsync(Operation, cancellationToken);
        }
        catch (ReadOnlyModeException)
        {
            await AuditRefusalAsync(adminId, null, requestId, cancellationToken);
            throw;
        }

        // 2: the address itself (spec VR-001).
        var trimmed = email.Trim();
        if (!IsWellFormedAddress(trimmed))
        {
            return Refused(DeanAccountRefusal.NotAnEmailAddress);
        }

        // 3: the school's domain, read server-side and never taken from the form (spec FR-004, S-04).
        var state = await legitimacyStates.GetForReadAsync(cancellationToken);
        var schoolDomain = string.IsNullOrEmpty(state?.Domain) ? null : state.Domain;
        if (schoolDomain is null || !IsInDomain(trimmed, schoolDomain))
        {
            return Refused(DeanAccountRefusal.OutsideSchoolDomain);
        }

        // 4: the password policy, against the address submitted as the login (spec FR-005).
        var violation = DeanPasswordPolicy.Check(temporaryPassword, trimmed);
        if (violation is { } broken)
        {
            return new DeanAccountActionOutcome(DeanAccountRefusal.PasswordPolicy, broken, null);
        }

        // 5: uniqueness, on the normalized form, whatever the role or state of the other account (spec VR-001).
        var normalized = AppUser.Normalize(trimmed);
        var existing = await users.FindByNormalizedEmailAsync(normalized, cancellationToken);
        if (existing is not null)
        {
            return Refused(DeanAccountRefusal.EmailAlreadyUsed);
        }

        var now = timeProvider.GetUtcNow();
        var dean = AppUser.CreateDean(
            trimmed,
            passwordHasher.Hash(temporaryPassword),
            schoolDefaults.UiLanguage,
            now);

        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                users.Add(dean);

                // The audit row names the account, so the identity has to exist first (US-008 db-design §4.4).
                await unitOfWork.SaveChangesAsync(token);
                auditEvents.Add(AuditEvent.DeanAccountManaged(
                    adminId,
                    AuditAction.DeanAccountCreated,
                    dean.Id,
                    now,
                    requestId));
                await unitOfWork.SaveChangesAsync(token);
            },
            cancellationToken);

        return new DeanAccountActionOutcome(null, null, dean.Id);
    }

    private static DeanAccountActionOutcome Refused(DeanAccountRefusal refusal) => new(refusal, null, null);

    /// <summary>
    /// The address shape of spec VR-001 — the same rule US-009 applies to the technical account: at most 254
    /// characters, no whitespace, exactly one <c>@</c>, a non-empty local part and a domain-shaped domain part.
    /// </summary>
    private static bool IsWellFormedAddress(string address)
    {
        if (address.Length is 0 or > ServiceAccountEmailAttribute.MaximumLength || address.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var parts = address.Split('@');
        return parts.Length == 2
            && parts[0].Length > 0
            && WorkspaceDomainAttribute.IsDomain(WorkspaceConnection.NormalizeDomain(parts[1]));
    }

    /// <summary>
    /// The address is in the school's domain when the two domains are equal. A subdomain of the school's domain
    /// is not the school's domain (spec FR-004).
    /// </summary>
    private static bool IsInDomain(string address, string schoolDomain) =>
        string.Equals(
            WorkspaceConnection.NormalizeDomain(address.Split('@')[1]),
            WorkspaceConnection.NormalizeDomain(schoolDomain),
            StringComparison.Ordinal);

    /// <summary>
    /// One audit row and nothing else, declared as <see cref="PermittedServiceWrite.AuditEvent"/> — on the
    /// BR-026 closed list — so the refusal is recorded in read-only mode too (spec FR-015, FR-017).
    /// </summary>
    private async Task AuditRefusalAsync(
        long adminId,
        long? deanId,
        string? requestId,
        CancellationToken cancellationToken)
    {
        auditEvents.Add(AuditEvent.DeanAccountManagementRefused(
            adminId,
            AuditAction.DeanAccountCreated,
            deanId,
            AuditRefusalCategory.ReadOnlyMode,
            timeProvider.GetUtcNow(),
            requestId));
        using (writeScope.Declare(PermittedServiceWrite.AuditEvent))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
