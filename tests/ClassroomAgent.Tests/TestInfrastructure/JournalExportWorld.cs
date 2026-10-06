using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Requests;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;
using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// US-028 over substituted ports (TC-1): the report world of US-027 / US-042 plus the text port, the renderer and a
/// unit of work that records which BR-026 service write was declared at each commit. No Google port exists in the
/// graph at all (spec FR-009).
/// </summary>
public sealed class JournalExportWorld
{
    public static readonly CultureInfo Uk = CultureInfo.GetCultureInfo("uk-UA");

    public JournalExportWorld(bool readOnly = false)
    {
        Report = new ReportTemplateWorld(readOnly);
        Work = new ScopeRecordingUnitOfWork(Report.Work, Report.WriteScope);
    }

    public ReportTemplateWorld Report { get; }

    public FakeReportTexts Texts { get; } = new();

    public FakeReportRenderer Renderer { get; } = new();

    public ScopeRecordingUnitOfWork Work { get; }

    public ExportJournalCommand Command =>
        new(Report.Fields, Report.Templates, Report.Zone, Report.Time, Texts, Renderer, Report.Audit, Work, Report.WriteScope);

    /// <summary>A course with one graded lesson in September and one student who has a submission graded 9 of 10.</summary>
    public long SeedCourse(string name = "Test Course One", string? section = "Test Section A")
    {
        var course = Report.Fields.AddCourse(name, section);
        var lesson = Report.Fields.AddLesson(course, new DateTimeOffset(2026, 9, 10, 9, 0, 0, TimeSpan.Zero), "Test Lesson One");
        var student = Report.Fields.AddMember(course, surname: "Тестова", givenName: "Олена", email: "olena.t@school-one.example.test");
        Report.Fields.AddMember(course, surname: "Тестовий", givenName: "Петро", email: "petro.t@school-one.example.test");
        Report.Fields.AddSubmission(lesson, student, SubmissionState.Returned, assignedGrade: 9m);
        return course;
    }

    public static JournalExportRequest Request(
        long? course,
        string? template = ReportTemplateTestData.BuiltInKey,
        string? from = JournalTestData.Period.FromText,
        string? to = JournalTestData.Period.ToText,
        string? names = null,
        string? orientation = null) =>
        new(template, course?.ToString(CultureInfo.InvariantCulture), from, to, names, orientation);

    public Task<JournalExportResult> RunAsync(JournalExportRequest request, long actorId = ReportTemplateWorld.DeanId, AppRole role = AppRole.Dean) =>
        Command.ExecuteAsync(request, actorId, role, Uk, "test-request-1", TestContext.Current.CancellationToken);

    /// <summary>Commits through the report world's unit of work, noting the declared service write of each.</summary>
    public sealed class ScopeRecordingUnitOfWork(ReportTemplateWorld.UnitOfWork inner, ServiceWriteScope scope) : IUnitOfWork
    {
        public List<PermittedServiceWrite?> Declared { get; } = [];

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Declared.Add(scope.Current);
            await inner.SaveChangesAsync(cancellationToken);
        }

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken) =>
            inner.ExecuteInTransactionAsync(work, cancellationToken);
    }
}
