namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>The ids <see cref="JournalHostExtensions.SeedJournalAsync"/> created, and the invented strings it used.</summary>
public sealed record SeededJournal(
    long CourseId,
    long GradedItemId,
    long UngradedItemId,
    long StudentId,
    long SilentStudentId,
    long TeacherId)
{
    public const string CourseName = "Test Course Journal";

    public const string Section = "Test Section A";

    public const string GradedTitle = "Test Graded Item";

    public const string UngradedTitle = "Test Ungraded Item";

    public const string MaterialTitle = "Test Material Item";

    public const string OctoberTitle = "Test October Item";

    public const string StudentName = "Test Student One";

    public const string SilentStudentName = "Test Student Two";

    public const string TeacherName = "Test Teacher One";
}
