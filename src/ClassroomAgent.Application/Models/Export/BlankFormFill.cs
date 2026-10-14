namespace ClassroomAgent.Application.Models.Export;

/// <summary>
/// US-043 spec FR-005, FR-006: the typed, translated fill model of the shipped form — every copy in file order with every
/// value already final. The renderer only places values at the markers (AD-3, AD-4).
/// </summary>
public sealed record BlankFormFill(IReadOnlyList<BlankFormCopy> Copies);
