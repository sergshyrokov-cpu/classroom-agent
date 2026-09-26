namespace ClassroomAgent.Application.Models;

/// <summary>Why a management action was refused (US-012 spec FR-003, FR-015, VR-001, VR-005).</summary>
public enum DeanAccountRefusal
{
    /// <summary>The installation is in read-only mode; evaluated first, before anything else (spec FR-015).</summary>
    ReadOnlyMode,

    /// <summary>The address is not an email, or its local part is empty (spec VR-001).</summary>
    NotAnEmailAddress,

    /// <summary>The address is not in the school's domain (spec FR-004).</summary>
    OutsideSchoolDomain,

    /// <summary>An account already exists for that normalized email — any role, any state (spec VR-001).</summary>
    EmailAlreadyUsed,

    /// <summary>The submitted password breaks the policy (spec FR-005).</summary>
    PasswordPolicy,

    /// <summary>No account with that id, or its role is not Dean — one answer for both (spec VR-005, I-8).</summary>
    NoSuchDeanAccount,

    /// <summary>The account is already in the requested state; a stale screen (spec VR-005).</summary>
    AlreadyInThatState,
}
