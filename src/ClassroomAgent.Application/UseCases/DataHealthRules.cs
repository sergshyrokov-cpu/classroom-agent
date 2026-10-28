using System.Globalization;
using ClassroomAgent.Application.Models;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Domain.Enums;
using ClassroomAgent.Domain.Rules;
using SkippedCourse = ClassroomAgent.Application.Models.Dtos.SkippedCourse;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-062 entity model §2.4: the one assessor both <see cref="GetDataHealthCountsQuery"/> and
/// <see cref="GetDataHealthQuery"/> compute the problems with, so the banner's counts and the page's totals agree by
/// construction (api-design §2.3). Spec FR-001: the problems are derived from stored data and the clock only — no Google
/// call, no write (FR-001.1) — and exist exactly while their condition holds (FR-001.4).
/// </summary>
internal static class DataHealthRules
{
    /// <summary>FR-001.3 X2 and X6: "more than 24 hours" — strictly more; exactly 24 hours raises nothing.</summary>
    public static readonly TimeSpan StalenessLimit = TimeSpan.FromHours(24);

    /// <summary>The stored facts, the read-only mode and the clock, read once per request.</summary>
    public static async Task<Assessment> AssessAsync(
        IDataHealthSource source,
        ISyncStateRepository syncStates,
        ISyncSkippedCourseRepository skippedCourses,
        ILegitimacyStateRepository legitimacyStates,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var facts = await source.GetCourseFactsAsync(cancellationToken);
        var state = await syncStates.GetForReadAsync(cancellationToken);
        var skips = await skippedCourses.GetAllAsync(cancellationToken);
        var legitimacy = await legitimacyStates.GetForReadAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var mode = GetLegitimacyModeQuery.Determine(legitimacy, now);

        var courseNames = StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: false);
        var entries = new List<CourseEntry>();
        var unknownState = new List<(CourseLabel Course, DateTimeOffset Since)>();
        var factsByGoogleId = facts.ToDictionary(f => f.GoogleId, StringComparer.Ordinal);
        var skipsByGoogleId = skips.ToDictionary(s => s.CourseGoogleId, StringComparer.Ordinal);

        foreach (var fact in facts)
        {
            skipsByGoogleId.TryGetValue(fact.GoogleId, out var skip);
            var archived = fact.State == CourseState.Archived;
            var label = new CourseLabel(fact.CourseId, fact.Name, null);
            var unknown = skip?.Reason == CourseSkipReason.UnknownCourseState;
            if (unknown && !archived)
            {
                unknownState.Add((label, skip!.SkippedSince));
            }

            var entry = new CourseEntry(
                label,
                archived,
                fact.AbsenceMark == CourseAbsenceMark.NotInClassroom ? fact.AbsentSince : null,
                skip?.Reason == CourseSkipReason.ValueOutOfRange
                    ? fact.LastCompleteReadAt is { } read
                        ? new SkippedCourse(SkippedCourseVariant.Since, read, null)
                        : new SkippedCourse(SkippedCourseVariant.NoDate, null, null)
                    : null,
                fact.HasItemNotInClassroom,
                fact.HasGradeAboveMaximum,
                unknown && archived);
            if (entry.HasAnyProblem)
            {
                entries.Add(entry);
            }
        }

        // FR-001.5, FR-001.6: a skip record with no stored course was never loaded — never archived, always counted.
        foreach (var skip in skips.Where(s => !factsByGoogleId.ContainsKey(s.CourseGoogleId)))
        {
            var label = new CourseLabel(null, skip.CourseName, skip.CourseName is null ? skip.CourseGoogleId : null);
            if (skip.Reason == CourseSkipReason.UnknownCourseState)
            {
                unknownState.Add((label, skip.SkippedSince));
            }
            else
            {
                entries.Add(new CourseEntry(
                    label,
                    false,
                    null,
                    new SkippedCourse(SkippedCourseVariant.NeverLoaded, null, skip.SkippedSince),
                    false,
                    false,
                    false));
            }
        }

        var technical = new List<TechnicalProblem>();
        if (state?.Status == SyncRunStatus.Failed)
        {
            technical.Add(new TechnicalProblem(
                TechnicalProblemKind.LastRunFailed,
                null,
                null,
                null,
                new TechnicalDetails(
                    DiagnosisOf(state.LastError), state.FailedStep?.ToString(), state.FinishedAt, null, null, null, null, null)));
        }

        if (!mode.IsReadOnly)
        {
            if (state?.LastSuccessfulRunAt is not { } lastSuccess)
            {
                technical.Add(new TechnicalProblem(
                    TechnicalProblemKind.NoSuccessYet,
                    null,
                    null,
                    null,
                    new TechnicalDetails(null, null, null, null, null, null, null, null)));
            }
            else if (now - lastSuccess > StalenessLimit)
            {
                technical.Add(new TechnicalProblem(
                    TechnicalProblemKind.NoRecentSuccess,
                    null,
                    null,
                    null,
                    new TechnicalDetails(null, null, null, lastSuccess, null, null, null, null)));
            }

            // FR-001.3 X6: the same instant and grace period as GetLegitimacyModeQuery; I-9: upgrade_required is a cause.
            if (mode.LastSuccessfulCheckAt is { } lastCheck && now - lastCheck > StalenessLimit)
            {
                technical.Add(new TechnicalProblem(
                    TechnicalProblemKind.ControlPlaneUnreachable,
                    null,
                    lastCheck,
                    lastCheck + GracePeriod.Length,
                    new TechnicalDetails(
                        null,
                        null,
                        null,
                        null,
                        legitimacy?.Compatibility == CompatibilityState.UpgradeRequired,
                        null,
                        null,
                        null)));
            }
        }

        if (state?.CourseListEmptySince is { } emptySince)
        {
            technical.Add(new TechnicalProblem(TechnicalProblemKind.CourseListEmpty, emptySince, null, null, null));
        }

        // FR-001.5: one problem for every course the technical account does not see, archived included.
        var notVisible = facts
            .Where(f => f.AbsenceMark == CourseAbsenceMark.NotVisibleToTechnicalAccount)
            .OrderBy(f => f.Name, courseNames)
            .ThenBy(f => f.CourseId)
            .Select(f => new NotVisibleCourse(
                f.CourseId, f.Name, f.State.ToString(), f.AbsentSince ?? now, f.LastCompleteReadAt))
            .ToList();
        if (notVisible.Count > 0)
        {
            technical.Add(new TechnicalProblem(
                TechnicalProblemKind.CoursesNotVisible,
                null,
                null,
                null,
                new TechnicalDetails(null, null, null, null, null, null, null, notVisible)));
        }

        foreach (var (course, since) in unknownState
            .OrderBy(u => u.Course.Name is null ? 1 : 0)
            .ThenBy(u => u.Course.Name ?? u.Course.GoogleCourseId, courseNames)
            .ThenBy(u => u.Course.CourseId))
        {
            technical.Add(new TechnicalProblem(
                TechnicalProblemKind.UnknownCourseState,
                null,
                null,
                null,
                new TechnicalDetails(null, null, null, null, null, course, since, null)));
        }

        var ordered = entries
            .OrderBy(e => e.Course.Name is null ? 1 : 0)
            .ThenBy(e => e.Course.Name ?? e.Course.GoogleCourseId, courseNames)
            .ThenBy(e => e.Course.CourseId)
            .ToList();
        return new Assessment(mode, technical, ordered);
    }

    /// <summary>VR-004: the stored error exactly as a declared <see cref="SyncDiagnosis"/> name, else Unexpected.</summary>
    private static string DiagnosisOf(string? lastError) =>
        Enum.GetNames<SyncDiagnosis>().FirstOrDefault(name => string.Equals(name, lastError, StringComparison.Ordinal))
        ?? nameof(SyncDiagnosis.Unexpected);

    /// <summary>The problems of one request: technical ones shaped for the Admin, and the per-course ones.</summary>
    internal sealed record Assessment(
        LegitimacyMode Mode, IReadOnlyList<TechnicalProblem> Technical, IReadOnlyList<CourseEntry> Courses)
    {
        /// <summary>FR-001.5: every technical problem counts one — X3 of an archived course is not among them.</summary>
        public int TechnicalCount => Technical.Count;

        /// <summary>FR-001.5: one per course and teaching kind; an archived course's problems are not counted.</summary>
        public int TeachingCount => Courses.Where(c => !c.Archived).Sum(c => c.TeachingKinds);
    }

    /// <summary>
    /// One course's per-course problems (FR-001.2, X3 of an archived course): T4 since, T3, T2, T1 and the archived X3.
    /// </summary>
    internal sealed record CourseEntry(
        CourseLabel Course,
        bool Archived,
        DateTimeOffset? NotInClassroomSince,
        SkippedCourse? Skipped,
        bool HasItemNotInClassroom,
        bool HasGradeAboveMaximum,
        bool ArchivedUnknownState)
    {
        public int TeachingKinds =>
            (NotInClassroomSince is null ? 0 : 1)
            + (Skipped is null ? 0 : 1)
            + (HasItemNotInClassroom ? 1 : 0)
            + (HasGradeAboveMaximum ? 1 : 0);

        public bool HasAnyProblem => TeachingKinds > 0 || ArchivedUnknownState;
    }
}
