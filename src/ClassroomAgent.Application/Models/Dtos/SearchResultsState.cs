namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-071 api-design D-2: no text or too short, refused by VR-001, or searched.</summary>
public enum SearchResultsState
{
    Prompt,

    Refused,

    Results,
}
