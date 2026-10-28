namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>Openapi US-038 <c>ErasureMessageKey</c>: the one-time message after a redirect; carries no personal data.</summary>
public enum ErasureMessageKey
{
    Erased,
    ExclusionLifted,

    /// <summary>US-065 spec FR-003: an exclusion was entered by email.</summary>
    ExclusionEntered,
}
