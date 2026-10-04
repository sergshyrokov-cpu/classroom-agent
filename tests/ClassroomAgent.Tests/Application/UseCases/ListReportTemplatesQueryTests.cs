using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-027 spec FR-002, FR-014, AC-001, AC-002, AC-014, api-design §2.2: the template list — the built-in first, the
/// created templates in the interface language's collation then by id, the author, the last change in the school's
/// time zone and the confirmation passed through.
/// </summary>
public sealed class ListReportTemplatesQueryTests
{
    private static readonly CultureInfo Ukrainian = CultureInfo.GetCultureInfo("uk-UA");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>AC-001: the built-in template is first, marked, without author or date, and cannot be changed.</summary>
    [Fact]
    public async Task TheBuiltInTemplate_IsFirst_MarkedBuiltIn_WithoutAuthorOrDate()
    {
        var world = new ReportTemplateWorld();
        world.SeedTemplate("Alpha");

        var page = await world.List.ExecuteAsync(Ukrainian, null, Ct);

        var builtIn = page.Templates[0];
        Assert.Equal(ReportTemplateTestData.BuiltInKey, builtIn.Reference);
        Assert.True(builtIn.IsBuiltIn);
        Assert.Null(builtIn.Name);
        Assert.Null(builtIn.AuthorState);
        Assert.Null(builtIn.AuthorEmail);
        Assert.Null(builtIn.ChangedAt);
        Assert.False(builtIn.CanChange);
        Assert.Equal(2, page.Templates.Count);
    }

    /// <summary>FR-002: with no created template the list is the built-in alone.</summary>
    [Fact]
    public async Task WithNoCreatedTemplate_TheListIsTheBuiltInAlone()
    {
        var world = new ReportTemplateWorld();

        var page = await world.List.ExecuteAsync(Ukrainian, null, Ct);

        Assert.Single(page.Templates);
        Assert.True(page.Templates[0].IsBuiltIn);
        Assert.Null(page.MessageKey);
    }

    /// <summary>AC-002: created templates by name in the Ukrainian collation, ties by id; author, change time, can change.</summary>
    [Fact]
    public async Task CreatedTemplates_AreOrderedByName_ThenId_WithTheirAuthor()
    {
        var world = new ReportTemplateWorld();
        var beta = world.SeedTemplate("Бета", authorId: 601, authorEmail: "dean.beta@school-one.example.test");
        var lower = world.SeedTemplate("альфа", authorId: 602, authorEmail: "dean.lower@school-one.example.test");
        var upper = world.SeedTemplate("Альфа", authorId: 603, authorEmail: "dean.upper@school-one.example.test");
        var tieFirst = world.SeedTemplate("Гама", authorId: 604, authorEmail: "dean.tie@school-one.example.test");
        var tieSecond = world.SeedTemplate("Гама", authorId: 604, authorEmail: "dean.tie@school-one.example.test");

        var page = await world.List.ExecuteAsync(Ukrainian, null, Ct);

        var created = page.Templates.Skip(1).ToList();
        Assert.Equal(
            new[] { lower.Id, upper.Id, beta.Id, tieFirst.Id, tieSecond.Id }.Select(id => id.ToString()),
            created.Select(t => t.Reference));
        Assert.Equal(new[] { "альфа", "Альфа", "Бета", "Гама", "Гама" }, created.Select(t => t.Name));
        Assert.All(created, t =>
        {
            Assert.False(t.IsBuiltIn);
            Assert.True(t.CanChange);
            Assert.Equal(ReportAuthorState.Known, t.AuthorState);
        });
        Assert.Equal("dean.lower@school-one.example.test", created[0].AuthorEmail);
        Assert.Equal("dean.beta@school-one.example.test", created[2].AuthorEmail);
    }

    /// <summary>FR-002, TC-8: the last change is shown as a school-local date and time — UTC+3 in September.</summary>
    [Fact]
    public async Task TheChangeTime_IsShownInKyiv()
    {
        var world = new ReportTemplateWorld();
        var summer = world.SeedTemplate("Summer");
        var winter = world.SeedTemplate("Winter");
        world.Templates.SetUpdatedAt(summer, new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));
        world.Templates.SetUpdatedAt(winter, new DateTimeOffset(2026, 1, 10, 12, 0, 0, TimeSpan.Zero));

        var page = await world.List.ExecuteAsync(Ukrainian, null, Ct);

        Assert.Equal(new DateTime(2026, 9, 10, 15, 0, 0), page.Templates.Single(t => t.Name == "Summer").ChangedAt);
        Assert.Equal(new DateTime(2026, 1, 10, 14, 0, 0), page.Templates.Single(t => t.Name == "Winter").ChangedAt);
    }

    /// <summary>FR-014, AC-014: a template whose author's account was deleted is listed as such, with no email.</summary>
    [Fact]
    public async Task ATemplateWhoseAuthorWasDeleted_IsListedAsAccountDeleted()
    {
        var world = new ReportTemplateWorld();
        world.SeedTemplate("Orphan", authorId: 777, authorEmail: "gone@school-one.example.test");
        world.SeedTemplate("Kept", authorId: ReportTemplateWorld.DeanId);
        world.Templates.DeletedAuthors.Add(777);

        var page = await world.List.ExecuteAsync(Ukrainian, null, Ct);

        var orphan = page.Templates.Single(t => t.Name == "Orphan");
        Assert.Equal(ReportAuthorState.AccountDeleted, orphan.AuthorState);
        Assert.Null(orphan.AuthorEmail);
        Assert.True(orphan.CanChange);
        Assert.Equal(ReportAuthorState.Known, page.Templates.Single(t => t.Name == "Kept").AuthorState);
    }

    /// <summary>FR-017: the confirmation key is passed through; the list itself carries no message.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(ReportTemplateConfirmationKey.Created)]
    [InlineData(ReportTemplateConfirmationKey.Changed)]
    [InlineData(ReportTemplateConfirmationKey.Deleted)]
    public async Task TheConfirmation_IsPassedThrough(ReportTemplateConfirmationKey? key)
    {
        var world = new ReportTemplateWorld();

        var page = await world.List.ExecuteAsync(Ukrainian, key, Ct);

        Assert.Equal(key, page.ConfirmationKey);
        Assert.Null(page.MessageKey);
    }
}
