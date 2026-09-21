namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>One step of the check as the page renders it (openapi <c>AccessCheckStep</c>): data attributes and visible text.</summary>
public sealed record RenderedStep(string? Kind, string? Scope, string? Outcome, string Text);
