namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The run-time half of a permitted-service-write declaration (US-007 spec FR-005): a use case opens it
/// around its commit and the backstop reads it. Scoped to the DI scope, at most one declaration open at a
/// time, cleared on dispose. It holds a <see cref="PermittedServiceWrite"/> value only - never a request,
/// a payload or personal data.
/// </summary>
public sealed class ServiceWriteScope
{
    /// <summary>The declaration currently open, or null when the default - refusal - applies.</summary>
    public PermittedServiceWrite? Current => throw new NotImplementedException();

    /// <summary>Declares the write about to be committed; disposing the result clears the declaration.</summary>
    public IDisposable Declare(PermittedServiceWrite write) => throw new NotImplementedException();
}
