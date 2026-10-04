using System.Globalization;
using ClassroomAgent.Application.Exceptions;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Web.UseCases;

/// <summary>
/// US-027 AC-006 / AC-011 (spec FR-013, FR-016): in every read-only cause the Application layer refuses create,
/// change and delete of a template before anything is written, leaving one refused audit row each; viewing — the list,
/// the forms and the report — works and never reaches Google. Proved on the use cases resolved from the host (TC-5).
/// </summary>
public sealed class ReportTemplateReadOnlyTests(PostgreSqlFixture database)
{
    public static TheoryData<ReadOnlyModeHost.Cause> ReadOnlyCauses => ReadOnlyModeHost.ReadOnlyCauses;

    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task WritesAreRefusedInApplication_InEveryReadOnlyCause(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);
        await host.InsertDeanAsync(ct);
        var actor = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct);
        var templateId = await host.InsertTemplateAsync(ct, "Test Existing", actor);
        var reference = templateId.ToString(CultureInfo.InvariantCulture);
        var before = await host.TemplateFingerprintAsync(ct);
        var form = ReportTemplateFormBuilder.Valid("Test Created").Input();

        await ReadOnlyModeHost.InScopeAsync<SaveReportTemplateUseCase>(host, async save =>
        {
            await Assert.ThrowsAsync<ReadOnlyModeException>(
                () => save.CreateAsync(actor, AppRole.Dean, form, "r-1", ct));
            await Assert.ThrowsAsync<ReadOnlyModeException>(
                () => save.ChangeAsync(actor, AppRole.Dean, reference, form, "r-2", ct));
        });
        await ReadOnlyModeHost.InScopeAsync<DeleteReportTemplateUseCase>(host, async delete =>
            await Assert.ThrowsAsync<ReadOnlyModeException>(
                () => delete.ExecuteAsync(actor, AppRole.Dean, reference, "r-3", ct)));

        Assert.Equal(before, await host.TemplateFingerprintAsync(ct));
        var audit = (await host.AuditRowsAsync(ct)).Where(r => r.Action.StartsWith("report_template_", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, audit.Count);
        Assert.All(audit, r =>
        {
            Assert.Equal("refused", r.Outcome);
            Assert.Equal("read_only_mode", r.RefusalCategory);
            Assert.Equal("report_template", r.TargetType);
            Assert.Equal(actor, r.ActorId);
        });
        Assert.Equal(
            ["report_template_created", "report_template_changed", "report_template_deleted"],
            audit.Select(r => r.Action).ToArray());
        Assert.Null(audit[0].TargetId);
        Assert.Equal(templateId, audit[1].TargetId);
        Assert.Equal(templateId, audit[2].TargetId);
    }

    [Theory]
    [MemberData(nameof(ReadOnlyCauses))]
    public async Task ViewingWorksInEveryReadOnlyCause_WithoutGoogle(ReadOnlyModeHost.Cause cause)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ReadOnlyModeHost.StartAsync(database, cause, ct);
        var seeded = await host.SeedJournalAsync(ct);
        await host.InsertDeanAsync(ct);
        var actor = await host.AccountIdAsync(DeanAccountTestData.DeanEmail, ct);
        var templateId = await host.InsertTemplateAsync(ct, "Test Existing", actor);
        var reference = templateId.ToString(CultureInfo.InvariantCulture);
        var culture = CultureInfo.GetCultureInfo("uk");
        var before = await host.TemplateFingerprintAsync(ct);
        var teaching = await host.TeachingFingerprintAsync(ct);
        ReportTemplateFormResult? edit = null;
        ReportPageResult? report = null;

        await ReadOnlyModeHost.InScopeAsync<ListReportTemplatesQuery>(host, async list =>
        {
            var page = await list.ExecuteAsync(culture, null, ct);

            Assert.Equal(2, page.Templates.Count);
            Assert.True(page.Templates[0].IsBuiltIn);
        });
        await ReadOnlyModeHost.InScopeAsync<GetReportTemplateFormQuery>(host, async forms =>
        {
            Assert.Equal(ClassroomAgent.Application.Models.Dtos.ReportTemplateFormMode.Create, forms.New().Mode);
            edit = await forms.EditAsync(reference, ct);
        });
        await ReadOnlyModeHost.InScopeAsync<GetReportQuery>(host, async query =>
            report = await query.ExecuteAsync(
                new ReportRequest(
                    [ReportTemplateTestData.BuiltInKey],
                    [seeded.CourseId.ToString(CultureInfo.InvariantCulture)],
                    [JournalTestData.Period.FromText],
                    [JournalTestData.Period.ToText]),
                culture,
                ct));

        Assert.NotNull(edit);
        Assert.Equal(ReportTemplateOutcome.Succeeded, edit.Outcome);
        Assert.NotNull(report);
        Assert.Equal(ReportPageOutcome.Shown, report.Outcome);
        Assert.NotNull(report.Page.Report);
        Assert.Equal(before, await host.TemplateFingerprintAsync(ct));
        Assert.Equal(teaching, await host.TeachingFingerprintAsync(ct));
        Assert.DoesNotContain(
            await host.AuditRowsAsync(ct), r => r.Action.StartsWith("report_template_", StringComparison.Ordinal));
        Assert.Empty(host.Classroom.ImpersonatedAs);
    }
}
