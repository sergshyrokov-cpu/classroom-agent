namespace ClassroomAgent.Application.Models.Dtos;

/// <summary>View DTO of the journal page (US-025 openapi <c>JournalPageModel</c>); no entity crosses into it (AD-8).</summary>
public sealed record JournalPageModel(
    IReadOnlyList<CourseOption> Courses,
    bool NoCoursesStored,
    long? SelectedCourseId,
    DateOnly? From,
    DateOnly? To,
    JournalView View,
    IReadOnlyList<JournalMessageKey> MessageKeys,
    JournalEmptyStateKey? EmptyStateKey,
    Journal? Journal);
