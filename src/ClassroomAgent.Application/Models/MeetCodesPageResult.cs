using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>The page or, for an invalid query, the page at its defaults with <c>QueryInvalid</c> (US-032 VR-004: answered <c>400</c>).</summary>
public sealed record MeetCodesPageResult(bool QueryInvalid, MeetCodesPageModel Model);
