using ClassroomAgent.Application.Models.Dtos;

namespace ClassroomAgent.Application.Models;

/// <summary>US-038 spec FR-009: the outcome, the VR-003 field error when invalid, and how many exclusions were removed.</summary>
public sealed record ExclusionLiftingResult(ExclusionLiftingOutcome Outcome, LiftFieldError? FieldError, int Lifted);
