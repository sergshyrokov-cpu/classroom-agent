namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-038 <c>SearchFieldError</c> (spec VR-001).</summary>
public enum SearchFieldError
{
    TermRequired,
    TermTooShort,
    TermTooLong,
}
