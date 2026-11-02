namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-068: one declaration of a style rule — a single selector of the rule's selector list (normalised by
/// <see cref="Styling.NormalizeSelector"/>), the property name in lower case and the raw value.
/// </summary>
public sealed record CssDeclaration(string Selector, string Property, string Value);
