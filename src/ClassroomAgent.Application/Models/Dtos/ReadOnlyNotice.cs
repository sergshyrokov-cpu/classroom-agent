using ClassroomAgent.Application.Models;

namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>US-062 OpenAPI <c>ReadOnlyNotice</c>: the read-only message shown at the top of the page (FR-004.5).</summary>
public sealed record ReadOnlyNotice(LegitimacyModeReason Reason);
