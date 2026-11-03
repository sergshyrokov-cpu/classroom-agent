namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-070 VR-001: why a search text was refused. Translated as <c>Courses.Validation.&lt;value&gt;</c> on every list.</summary>
public enum SearchRefusalKey
{
    SearchRepeated,

    SearchTooLong,

    SearchControlCharacter,
}
