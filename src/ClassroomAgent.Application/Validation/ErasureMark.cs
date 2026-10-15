namespace ClassroomAgent.Application.Validation;

/// <summary>US-038 spec VR-002: only an explicit <c>true</c> (any letter case) sets the "erasure is irreversible" mark.</summary>
public static class ErasureMark
{
    public static bool IsSet(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
