using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-007 AC-007, first half: the structural rule is proven to <b>detect</b> a violation before it is run
/// over the real assembly. It is exercised against four synthetic types - a guarded write path, a guarded
/// Google path, a registered permitted write, and a write path with neither - and must flag exactly the
/// last one (spec FR-010).
/// </summary>
public sealed class WritePathRuleTests
{
    private static readonly IReadOnlyDictionary<Type, PermittedServiceWrite> Declared =
        new Dictionary<Type, PermittedServiceWrite>
        {
            [typeof(DeclaredServiceWriteUseCase)] = PermittedServiceWrite.LegitimacyCheckState,
        };

    [Fact]
    public void AWritePathWithNeitherGuardNorDeclaration_IsFlagged()
    {
        var violations = WritePathRule.Violations([typeof(UnguardedWriteUseCase)], Declared);

        var message = Assert.Single(violations);
        Assert.Contains(nameof(UnguardedWriteUseCase), message, StringComparison.Ordinal);
    }

    [Fact]
    public void AGooglePathWithoutTheGuard_IsFlagged()
    {
        var violations = WritePathRule.Violations([typeof(UnguardedGoogleUseCase)], Declared);

        Assert.Single(violations);
    }

    [Fact]
    public void AGuardedWritePath_IsAccepted()
    {
        Assert.Empty(WritePathRule.Violations([typeof(SyntheticWriteUseCase)], Declared));
    }

    [Fact]
    public void AGuardedGooglePath_IsAccepted()
    {
        Assert.Empty(WritePathRule.Violations([typeof(SyntheticGoogleUseCase)], Declared));
    }

    [Fact]
    public void ARegisteredServiceWrite_IsAccepted()
    {
        Assert.Empty(WritePathRule.Violations([typeof(DeclaredServiceWriteUseCase)], Declared));
    }

    [Fact]
    public void ARegisteredServiceWrite_IsFlagged_OnceItsDeclarationIsRemoved()
    {
        // Removing the declaration is the same mistake as removing the guard: the rule must notice both.
        var violations = WritePathRule.Violations(
            [typeof(DeclaredServiceWriteUseCase)],
            new Dictionary<Type, PermittedServiceWrite>());

        Assert.Single(violations);
    }

    [Fact]
    public void AllFourTogether_FlagOnlyTheViolations()
    {
        var violations = WritePathRule.Violations(
            [
                typeof(SyntheticWriteUseCase),
                typeof(SyntheticGoogleUseCase),
                typeof(DeclaredServiceWriteUseCase),
                typeof(UnguardedWriteUseCase),
                typeof(UnguardedGoogleUseCase),
            ],
            Declared);

        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, m => m.Contains(nameof(UnguardedWriteUseCase), StringComparison.Ordinal));
        Assert.Contains(violations, m => m.Contains(nameof(UnguardedGoogleUseCase), StringComparison.Ordinal));
    }

    [Fact]
    public void AReadUseCase_IsNotAProtectedPath()
    {
        // AC-005: the rule applies to writes and Google calls only; a read is never made to depend on it.
        Assert.False(WritePathRule.IsProtectedPath(typeof(GetLegitimacyModeQuery)));
        Assert.False(WritePathRule.IsProtectedPath(typeof(GetReadinessQuery)));
    }

    [Fact]
    public void TheFailureMessage_NamesTheTypeAndBothRemedies()
    {
        var message = WritePathRule.Message(typeof(UnguardedWriteUseCase));

        Assert.Contains(typeof(UnguardedWriteUseCase).FullName!, message, StringComparison.Ordinal);
        Assert.Contains(nameof(IReadOnlyModeGuard), message, StringComparison.Ordinal);
        Assert.Contains(nameof(PermittedServiceWrites), message, StringComparison.Ordinal);
        Assert.Contains("BR-026", message, StringComparison.Ordinal);
        Assert.Contains("trebovaniya.md", message, StringComparison.Ordinal);
    }
}
