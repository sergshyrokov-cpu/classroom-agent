namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>
/// The API-6 error body, returned for every failure under <c>/api/v1</c> (US-008 spec FR-014; api-design §5).
/// <see cref="Message"/> is safe to show a Dean or an Admin, in their language (NFR-073), and is never a stack
/// trace, SQL, a type name, a path, a Google error or a secret (SC-10, NFR-023).
/// </summary>
public sealed record ApiError(
    DateTimeOffset Timestamp,
    int Status,
    string Error,
    string Message,
    string Path,
    IReadOnlyList<ApiFieldError>? FieldErrors = null);

/// <summary>One rejected field of a validated request (API-6).</summary>
public sealed record ApiFieldError(string Field, string Message);
