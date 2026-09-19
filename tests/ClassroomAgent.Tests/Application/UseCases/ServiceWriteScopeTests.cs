using ClassroomAgent.Application.UseCases;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-007 VR-002: the run-time declaration of a permitted service write. At most one is open at a time, it
/// names a defined member of the closed list, and disposing it restores the default - refusal
/// (spec FR-005, AC-004).
/// </summary>
public sealed class ServiceWriteScopeTests
{
    [Fact]
    public void WithNoDeclaration_NothingIsPermitted()
    {
        var scope = new ServiceWriteScope();

        Assert.Null(scope.Current);
    }

    [Fact]
    public void ADeclaration_IsVisibleWhileItIsOpen()
    {
        var scope = new ServiceWriteScope();

        using var declaration = scope.Declare(PermittedServiceWrite.LegitimacyCheckState);

        Assert.Equal(PermittedServiceWrite.LegitimacyCheckState, scope.Current);
    }

    [Fact]
    public void Disposing_ClearsTheDeclaration()
    {
        var scope = new ServiceWriteScope();
        scope.Declare(PermittedServiceWrite.AuditEvent).Dispose();

        Assert.Null(scope.Current);
    }

    [Fact]
    public void ASecondDeclaration_BeforeTheFirstIsDisposed_IsRejected()
    {
        var scope = new ServiceWriteScope();
        using var first = scope.Declare(PermittedServiceWrite.LegitimacyCheckState);

        Assert.Throws<InvalidOperationException>(() => scope.Declare(PermittedServiceWrite.AuditEvent));
    }

    [Fact]
    public void AnUndefinedMember_IsRejected()
    {
        var scope = new ServiceWriteScope();

        Assert.Throws<ArgumentException>(() => scope.Declare((PermittedServiceWrite)99));
    }

    [Fact]
    public void DisposingTwice_IsHarmless()
    {
        var scope = new ServiceWriteScope();
        var declaration = scope.Declare(PermittedServiceWrite.RetentionPurge);

        declaration.Dispose();
        declaration.Dispose();

        Assert.Null(scope.Current);
    }
}
