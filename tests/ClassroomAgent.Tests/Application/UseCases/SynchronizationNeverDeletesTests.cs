using ClassroomAgent.Application.Models;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Tests.TestInfrastructure;

namespace ClassroomAgent.Tests.Application.UseCases;

/// <summary>
/// US-037 AC-013, spec FR-016, PC-11: a person who disappears from the roster in Google keeps their membership — marked
/// off the roster — and their submissions; only the purge deletes.
/// </summary>
public sealed class SynchronizationNeverDeletesTests
{
    private static RosterEntry Person(int ordinal) =>
        new(CourseTestData.UserId(ordinal), CourseTestData.Email($"person{ordinal}"), CourseTestData.Name(ordinal));

    [Fact]
    public async Task APersonWhoLeftTheRoster_KeepsTheirMembershipAndSubmissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var world = new SyncWorld();
        var courseId = CourseTestData.CourseId(1);
        var itemDate = world.Time.GetUtcNow();
        world.Classroom
            .WithCourse(courseId, CourseTestData.CourseName(1), students: [Person(1), Person(2)])
            .WithCourseWork(courseId, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), itemDate)
            .WithSubmission(courseId, CourseWorkTestData.ItemId(1), CourseTestData.UserId(1), CourseWorkTestData.SubmissionId(1), "TURNED_IN", assignedGrade: 10);
        await world.Run.ExecuteAsync(SyncWorld.RunId(1), ct);
        var course = world.Courses.Stored[courseId];
        var leaver = world.Participants.Stored[CourseTestData.UserId(1)];
        Assert.Single(world.Submissions.Stored);

        world.Time.Advance(SyncTestData.DefaultInterval);
        var withoutTheLeaver = new SyncWorld.ClassroomReader()
            .WithCourse(courseId, CourseTestData.CourseName(1), students: [Person(2)])
            .WithCourseWork(courseId, CourseWorkTestData.ItemId(1), CourseWorkResource.CourseWork, CourseWorkTestData.Title(1), itemDate);
        await world.RunUsing(withoutTheLeaver).ExecuteAsync(SyncWorld.RunId(2), ct);

        var membership = Assert.Single(world.Memberships.OfCourse(course.Id), m => m.ParticipantId == leaver.Id);
        Assert.False(membership.OnRoster);
        Assert.Single(world.Submissions.Stored);
        Assert.Contains(CourseTestData.UserId(1), world.Participants.Stored.Keys);
    }
}
