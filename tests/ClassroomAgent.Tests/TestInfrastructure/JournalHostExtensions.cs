using System.Net;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using ClassroomAgent.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// Drives US-025 over HTTP and over PostgreSQL: an installation host in a chosen legitimacy state with an Admin or a
/// Dean signed in (the US-019 pattern), a seeded September journal, the real <see cref="JournalSource"/> over the
/// host's database, and a fingerprint of the teaching tables for the "writes nothing" assertions. Nothing reaches
/// Google (TC-4): the host's <see cref="FakeClassroomReader"/> records any call.
/// </summary>
public static class JournalHostExtensions
{
    public enum Actor
    {
        Admin,
        Dean,

        /// <summary>A Dean whose password is temporary: the restricted session of US-012 api-design §2.6.</summary>
        DeanWithTemporaryPassword,

        /// <summary>No session at all.</summary>
        Anonymous,
    }

    /// <summary>The tables the journal reads (db-design §1); "writes nothing" is asserted on them.</summary>
    public static readonly string[] TeachingTables =
        ["course", "classroom_participant", "course_membership", "course_work", "submission"];

    /// <summary>A started installation in the given legitimacy state with the given actor signed in.</summary>
    public static async Task<(InstallationTestHost Host, FormClient Client)> StartAsync(
        PostgreSqlFixture database,
        Actor actor,
        CancellationToken cancellationToken,
        ReadOnlyModeHost.Cause cause = ReadOnlyModeHost.Cause.NotReadOnly,
        Action<InstallationTestHost>? beforeStart = null)
    {
        var host = await InstallationTestHost.CreateAsync(database, cancellationToken);
        await ReadOnlyModeHost.SeedAsync(host, cause, cancellationToken);
        host.ControlPlaneHandler = WorkspaceConnectionHostExtensions.ApprovingChannel();
        beforeStart?.Invoke(host);
        host.Start();
        return (host, await SignInAsync(host, actor, cancellationToken));
    }

    /// <summary>Signs the actor in on a new client of a started host.</summary>
    public static async Task<FormClient> SignInAsync(InstallationTestHost host, Actor actor, CancellationToken cancellationToken)
    {
        switch (actor)
        {
            case Actor.Admin:
                {
                    var (admin, callback) = await host.SignInWithGoogleAsync(cancellationToken);
                    Assert.Equal(HttpStatusCode.Redirect, callback.Status);
                    return admin;
                }

            case Actor.Dean:
            case Actor.DeanWithTemporaryPassword:
                {
                    var temporary = actor == Actor.DeanWithTemporaryPassword;
                    var password = temporary ? DeanAccountTestData.TemporaryPassword : JournalTestData.DeanPassword;
                    await host.InsertDeanAsync(cancellationToken, password: password, passwordIsTemporary: temporary);
                    var client = host.CreateClient();
                    var signIn = await client.SignInAsDeanAsync(DeanAccountTestData.DeanEmail, password, cancellationToken);
                    Assert.Equal(HttpStatusCode.Redirect, signIn.Status);
                    return client;
                }

            case Actor.Anonymous:
                return host.CreateClient();

            default:
                throw new ArgumentOutOfRangeException(nameof(actor), actor, null);
        }
    }

    /// <summary>
    /// Seeds one course with a September journal (spec FR-004 … FR-006; all titles and names invented, TC-4):
    /// a graded item (10 points, due before B), an ungraded item and a material, all dated in the period; one item
    /// dated in October (outside it); a named student with a late graded submission of 8.5 and a turned-in ungraded
    /// one; a student on the roster with no submission; a teacher who also has a submission (never a row).
    /// </summary>
    public static async Task<SeededJournal> SeedJournalAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        // The retention purge runs at every host start (US-037) and deletes a participant that has no membership yet;
        // seeding after its first run keeps it from racing the inserts below. It runs in every mode (BR-026).
        await host.WaitForPurgeRunsAsync(1, cancellationToken);

        var courseId = await CourseRows.InsertCourseAsync(host, cancellationToken, name: SeededJournal.CourseName, section: SeededJournal.Section);

        var graded = await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(1), title: SeededJournal.GradedTitle,
            itemDate: JournalTestData.Period.Early, dueAt: JournalTestData.Period.Early, maxPoints: 10m);
        var ungraded = await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(2), title: SeededJournal.UngradedTitle,
            itemDate: JournalTestData.Period.Early.AddDays(1));
        await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(3), title: SeededJournal.MaterialTitle,
            resource: CourseWorkTestData.ResourceCodes.CourseWorkMaterial, itemDate: JournalTestData.Period.Early.AddDays(2));
        await CourseWorkRows.InsertCourseWorkAsync(
            host, courseId, cancellationToken, googleId: CourseWorkTestData.ItemId(4), title: SeededJournal.OctoberTitle,
            itemDate: JournalTestData.Period.EndUtc.AddDays(3), maxPoints: 5m);

        var student = await CourseRows.InsertParticipantAsync(
            host, cancellationToken, googleUserId: CourseTestData.UserId(1), email: CourseTestData.Email("student.one"),
            fullName: SeededJournal.StudentName);
        await CourseRows.InsertMembershipAsync(
            host, courseId, student, cancellationToken, firstSeenAt: JournalTestData.Period.StartUtc.AddDays(-30));
        var silent = await CourseRows.InsertParticipantAsync(
            host, cancellationToken, googleUserId: CourseTestData.UserId(2), email: CourseTestData.Email("student.two"),
            fullName: SeededJournal.SilentStudentName);
        await CourseRows.InsertMembershipAsync(
            host, courseId, silent, cancellationToken, firstSeenAt: JournalTestData.Period.StartUtc.AddDays(-30));
        var teacher = await CourseRows.InsertParticipantAsync(
            host, cancellationToken, googleUserId: CourseTestData.UserId(3), email: CourseTestData.Email("teacher.one"),
            fullName: SeededJournal.TeacherName);
        await CourseRows.InsertMembershipAsync(
            host, courseId, teacher, cancellationToken, role: CourseTestData.Roles.Teacher,
            firstSeenAt: JournalTestData.Period.StartUtc.AddDays(-30));

        await CourseWorkRows.InsertSubmissionAsync(
            host, graded, student, cancellationToken, googleId: CourseWorkTestData.SubmissionId(1),
            state: CourseWorkTestData.StateCodes.TurnedIn, assignedGrade: 8.5m, late: true,
            turnedInAt: JournalTestData.Period.Early.AddHours(2));
        await CourseWorkRows.InsertSubmissionAsync(
            host, ungraded, student, cancellationToken, googleId: CourseWorkTestData.SubmissionId(2),
            state: CourseWorkTestData.StateCodes.TurnedIn);
        await CourseWorkRows.InsertSubmissionAsync(
            host, graded, teacher, cancellationToken, googleId: CourseWorkTestData.SubmissionId(3),
            state: CourseWorkTestData.StateCodes.TurnedIn, assignedGrade: 3m);

        return new SeededJournal(courseId, graded, ungraded, student, silent, teacher);
    }

    /// <summary>Runs <paramref name="body"/> with the real <see cref="JournalSource"/> over the host's database.</summary>
    public static async Task WithJournalSourceAsync(this InstallationTestHost host, Func<IJournalSource, ClassroomAgentDbContext, Task> body)
    {
        using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>();
        await body(new JournalSource(db), db);
    }

    /// <summary>
    /// Row count and the sum of every row's <c>xmin</c> per teaching table: any insert, update or delete changes it.
    /// </summary>
    public static async Task<string> TeachingFingerprintAsync(this InstallationTestHost host, CancellationToken cancellationToken)
    {
        var parts = new List<string>();
        foreach (var table in TeachingTables)
        {
            var value = await host.ScalarAsync<string>(
                $"SELECT count(*)::text || ':' || coalesce(sum(xmin::text::bigint), 0)::text FROM {table}",
                cancellationToken);
            parts.Add(table + "=" + value);
        }

        return string.Join(';', parts);
    }

    /// <summary>Whether the EF model has changes no migration captures (db-design §5: none is expected).</summary>
    public static bool HasPendingModelChanges(this InstallationTestHost host)
    {
        using var scope = host.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>().Database.HasPendingModelChanges();
    }
}
