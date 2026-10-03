using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-039 AC-003: choosing a language is the BR-026 permitted write, proven in the Application layer against the
/// real commit backstop and PostgreSQL (AD-6, TC-2, TC-5) — in each read-only cause of BR-025, and without opening
/// any other write path (spec FR-007; db-design §6).
/// </summary>
public sealed class UiLanguageReadOnlyTests(PostgreSqlFixture database)
{
    [Theory]
    [MemberData(nameof(ReadOnlyModeHost.ReadOnlyCauses), MemberType = typeof(ReadOnlyModeHost))]
    public async Task ChoosingALanguage_IsStoredInReadOnlyMode(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);
        await host.InsertAppUserAsync(ct, role: "admin");
        var admin = Assert.Single(await host.AppUsersAsync(ct));

        await ReadOnlyModeHost.InScopeAsync<ChooseUiLanguageUseCase>(
            host,
            useCase => useCase.ExecuteAsync(admin.Id, UiLanguageTestData.English, ct));

        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(admin.Id, ct));
    }

    /// <summary>
    /// AC-003: "it is the BR-026 permitted write and nothing else". In the very scope that just chose a language, an
    /// undeclared write is still refused — the declaration did not stay open.
    /// </summary>
    [Fact]
    public async Task AfterTheChoice_AnUndeclaredWriteInTheSameScope_IsStillRefused()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.Suspended, ct);
        await host.InsertAppUserAsync(ct, role: "admin");
        var admin = Assert.Single(await host.AppUsersAsync(ct));
        var before = await host.LegitimacyStatesAsync(ct);

        using var scope = host.CreateScope();
        var chosen = await scope.ServiceProvider.GetRequiredService<ChooseUiLanguageUseCase>()
            .ExecuteAsync(admin.Id, UiLanguageTestData.English, ct);
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => scope.ServiceProvider
            .GetRequiredService<UnguardedWriteUseCase>()
            .ExecuteAsync(host.Time.GetUtcNow(), ct));

        Assert.NotNull(chosen);
        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(admin.Id, ct));
        Assert.Equal(before, await host.LegitimacyStatesAsync(ct));
    }

    /// <summary>The same write outside read-only mode — the control the read-only cases are measured against.</summary>
    [Fact]
    public async Task OutsideReadOnlyMode_ChoosingALanguage_IsStored()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, ReadOnlyModeHost.Cause.NotReadOnly, ct);
        await host.InsertAppUserAsync(ct, role: "admin");
        var admin = Assert.Single(await host.AppUsersAsync(ct));

        await ReadOnlyModeHost.InScopeAsync<ChooseUiLanguageUseCase>(
            host,
            useCase => useCase.ExecuteAsync(admin.Id, UiLanguageTestData.English, ct));

        Assert.Equal(UiLanguageTestData.English, await host.StoredLanguageAsync(admin.Id, ct));
    }

    /// <summary>FR-007: the registry gains exactly this use case, as sign-in bookkeeping.</summary>
    [Fact]
    public void TheRegistry_DeclaresTheLanguageChoiceAsSignInBookkeeping()
    {
        var declaration = Assert.Contains(typeof(ChooseUiLanguageUseCase), PermittedServiceWrites.Declarations);

        Assert.Equal(PermittedServiceWrite.SignInBookkeeping, declaration);
    }
}
