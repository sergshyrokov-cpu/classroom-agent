using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Ls = ClassroomAgent.Tests.TestInfrastructure.ListSearchTestData;
using Mc = ClassroomAgent.Tests.TestInfrastructure.MeetCodesHostExtensions;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-071: seeds of the header search, by SQL over real PostgreSQL. Every value is invented (TC-4).</summary>
public static class SearchHostExtensions
{
    public static readonly DateTimeOffset At = new(2026, 9, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The ids behind <see cref="SeedOneOfEachAsync"/>.</summary>
    public sealed record OneOfEach(long CourseId, long StudentId, long TeacherId);

    public const string StudentSurname = Ls.Needle + "студ";

    public const string TeacherSurname = Ls.Needle + "чук";

    public const string CourseName = Ls.Needle + " курс";

    public const string Code = Ls.Needle + "-aaaa";

    /// <summary>One row in each of the four groups, each holding <see cref="Ls.Needle"/>, and one row of no group's match.</summary>
    public static async Task<OneOfEach> SeedOneOfEachAsync(this InstallationTestHost host, CancellationToken ct)
    {
        await host.PrepareAsync(ct);
        var course = await host.InsertCourseAsync(1, CourseName, ct);
        var student = await host.InsertPersonAsync(1, StudentSurname, "Анна", CourseTestData.Email("zz.student"), ct);
        var teacher = await host.InsertPersonAsync(2, TeacherSurname, "Олена", CourseTestData.Email("zz.teacher"), ct);
        await host.AddStudentAsync(course, student, ct);
        await host.AddTeacherAsync(course, teacher, ct);
        await host.SeedMeetingAsync(Code, Mc.Email("org.one"), At, ct);

        var other = await host.InsertCourseAsync(2, "Інший предмет", ct);
        var otherStudent = await host.InsertPersonAsync(3, "Шевчук", "Тарас", CourseTestData.Email("other.student"), ct);
        var otherTeacher = await host.InsertPersonAsync(4, "Коваль", "Ірина", CourseTestData.Email("other.teacher"), ct);
        await host.AddStudentAsync(other, otherStudent, ct);
        await host.AddTeacherAsync(other, otherTeacher, ct);
        await host.SeedMeetingAsync("oth-erco-dee", Mc.Email("org.two"), At, ct);
        return new OneOfEach(course, student, teacher);
    }

    /// <summary>Students, all of one course, surnamed <c>Зразок01</c>…; returns their ids in that order.</summary>
    public static async Task<IReadOnlyList<long>> SeedNumberedStudentsAsync(this InstallationTestHost host, int count, CancellationToken ct, int firstOrdinal = 100)
    {
        var course = await host.InsertCourseAsync(firstOrdinal, "Спільний предмет", ct);
        var ids = new List<long>();
        for (var i = 1; i <= count; i++)
        {
            var person = await host.InsertPersonAsync(firstOrdinal + i, $"Зразок{i:D2}", "Учень", CourseTestData.Email($"s{firstOrdinal + i}"), ct);
            await host.AddStudentAsync(course, person, ct);
            ids.Add(person);
        }

        return ids;
    }

    /// <summary>Teachers, all of one course, surnamed <c>Зразок01</c>…; returns their ids in that order.</summary>
    public static async Task<IReadOnlyList<long>> SeedSampleTeachersAsync(this InstallationTestHost host, int count, CancellationToken ct, int firstOrdinal = 300)
    {
        var course = await host.InsertCourseAsync(firstOrdinal, "Предмет викладачів", ct);
        var ids = new List<long>();
        for (var i = 1; i <= count; i++)
        {
            var person = await host.InsertPersonAsync(firstOrdinal + i, $"Зразок{i:D2}", "Викладач", CourseTestData.Email($"t{firstOrdinal + i}"), ct);
            await host.AddTeacherAsync(course, person, ct);
            ids.Add(person);
        }

        return ids;
    }

    /// <summary>Courses named <c>Зразок01</c>…; returns their ids in that order.</summary>
    public static async Task<IReadOnlyList<long>> SeedSampleCoursesAsync(this InstallationTestHost host, int count, CancellationToken ct, int firstOrdinal = 500)
    {
        var ids = new List<long>();
        for (var i = 1; i <= count; i++)
        {
            ids.Add(await host.InsertCourseAsync(firstOrdinal + i, $"Зразок{i:D2}", ct));
        }

        return ids;
    }

    /// <summary>
    /// Meet codes <c>zzm-NNNN-aaa</c> spread over the three lists, numbered across them in the order the arguments give;
    /// a linked one is linked to a course not matching the text.
    /// </summary>
    public static async Task SeedSampleCodesAsync(this InstallationTestHost host, int unassigned, int linked, int marked, CancellationToken ct)
    {
        await host.InsertAppUserAsync(ct, email: Mc.Email("code.maker"));
        var maker = await host.AccountIdAsync(Mc.Email("code.maker"), ct);
        var course = await host.SeedRosterCourseAsync("Предмет кодів", [Mc.Email("codes.teacher")], [], ct);
        var number = 0;
        foreach (var (count, list) in new[] { (unassigned, "u"), (linked, "l"), (marked, "m") })
        {
            for (var i = 0; i < count; i++)
            {
                var code = $"zzm-{++number:D4}-aaa";
                await host.SeedMeetingAsync(code, Mc.Email("org.one"), At, ct);
                if (list == "l")
                {
                    await host.SeedPersonLinkAsync(code, course, maker, At, ct);
                }
                else if (list == "m")
                {
                    await host.SeedMarkAsync(code, maker, At, ct);
                }
            }
        }
    }

    /// <summary>Runs <paramref name="body"/> with the real student source over the host's database.</summary>
    public static async Task WithStudentSearchSourceAsync(this InstallationTestHost host, Func<IStudentSearchSource, Task> body)
    {
        using var scope = host.CreateScope();
        await body(new StudentSearchSource(scope.ServiceProvider.GetRequiredService<ClassroomAgentDbContext>()));
    }

    /// <summary>The ids of the students found, in order, after checking that the count agrees with them.</summary>
    public static async Task<IReadOnlyList<long>> StudentsFoundAsync(this InstallationTestHost host, string text, CancellationToken ct)
    {
        IReadOnlyList<long> found = [];
        await host.WithStudentSearchSourceAsync(async source =>
        {
            found = (await source.GetStudentsAsync(text, 100, ct)).Select(r => r.ParticipantId).ToList();
            Assert.Equal(found.Count, await source.CountStudentsAsync(text, ct));
        });
        return found;
    }

    /// <summary>The courses of the given students, as the source gives them.</summary>
    public static async Task<IReadOnlyList<StudentSearchCourseRecord>> StudentCoursesAsync(this InstallationTestHost host, IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        IReadOnlyList<StudentSearchCourseRecord> courses = [];
        await host.WithStudentSearchSourceAsync(async source => courses = await source.GetStudentCoursesAsync(ids, ct));
        return courses;
    }

    /// <summary>The first matches of the Meet codes, as the source gives them.</summary>
    public static async Task<IReadOnlyList<MeetCodeMatch>> CodesMatchedAsync(this InstallationTestHost host, string text, int limit, CancellationToken ct)
    {
        IReadOnlyList<MeetCodeMatch> found = [];
        await host.WithMeetCodesReadSourceAsync(async source => found = await source.GetFirstMatchesAsync(text, limit, ct));
        return found;
    }
}
