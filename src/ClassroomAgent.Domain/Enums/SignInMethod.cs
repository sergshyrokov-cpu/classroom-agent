namespace ClassroomAgent.Domain.Enums;

/// <summary>How an account signs in (US-008 entity model §2.3): Google OAuth for an Admin, a password for a Dean.</summary>
public enum SignInMethod
{
    Google,
    Password,
}
