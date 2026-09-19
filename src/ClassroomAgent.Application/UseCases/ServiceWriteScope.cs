namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// The run-time half of a permitted-service-write declaration (US-007 spec FR-005): a use case opens it
/// around its commit and the backstop reads it. Scoped to the DI scope, at most one declaration open at a
/// time, cleared on dispose. It holds a <see cref="PermittedServiceWrite"/> value only - never a request,
/// a payload or personal data.
/// </summary>
public sealed class ServiceWriteScope
{
    private PermittedServiceWrite? _current;

    /// <summary>The declaration currently open, or null when the default - refusal - applies.</summary>
    public PermittedServiceWrite? Current => _current;

    /// <summary>Declares the write about to be committed; disposing the result clears the declaration.</summary>
    public IDisposable Declare(PermittedServiceWrite write)
    {
        if (!Enum.IsDefined(write))
        {
            throw new ArgumentException("Not a member of the BR-026 closed list.", nameof(write));
        }

        if (_current is not null)
        {
            throw new InvalidOperationException("A permitted service write is already declared in this scope.");
        }

        _current = write;
        return new Declaration(this);
    }

    private sealed class Declaration(ServiceWriteScope scope) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            scope._current = null;
        }
    }
}
