using System.Globalization;
using ClassroomAgent.Application.Models.Dtos;
using ClassroomAgent.Application.Models.Export;
using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// US-043 spec FR-003, FR-005, FR-006 (fill method Basic): lays the report of the page out over copies of the shipped
/// form "Бланк для групових занять". The form is known by its markers and three capacities, never by a cell address
/// (OD-008 a). Every value is the screen's — grade cells exactly as US-028 types them — nothing is computed again.
/// </summary>
public static class GroupLessonsBlankMapper
{
    /// <summary>Spec FR-003: rows from the student-marker row downward.</summary>
    public const int StudentRows = 30;

    /// <summary>Spec FR-003: columns from the lesson-marker column rightward; the last one is kept for a final grade.</summary>
    public const int LessonColumns = 24;

    /// <summary>Spec FR-003: rows from the topic-marker row downward.</summary>
    public const int TopicRows = 23;

    /// <summary>Spec VR-001: the per-copy limits are the capacities (23 = lesson columns minus the final-grade column).</summary>
    public const int StudentsPerCopyMax = StudentRows;

    public const int LessonsPerCopyMax = LessonColumns - 1;

    /// <summary>Spec VR-002, OD-011 a: the most sheets one file may have.</summary>
    public const int SheetLimit = 100;

    /// <summary>Openapi <c>BlankFormInputs</c>: the page's defaults are the maximums (spec FR-004).</summary>
    public static BlankFormInputs Inputs { get; } =
        new(StudentsPerCopyMax, StudentsPerCopyMax, LessonsPerCopyMax, LessonsPerCopyMax);

    /// <summary>Spec FR-005.1: the report's students, in report order; none in the "nothing published" state.</summary>
    public static int StudentCount(Report report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return Grading(report)?.Rows.Count ?? 0;
    }

    /// <summary>Spec FR-005.1: the lessons are the report's non-material items — exactly the "Lesson topics" rows.</summary>
    public static int LessonCount(Report report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return Topics(report).Count;
    }

    /// <summary>Spec FR-006: groups × blocks "Оцінювання" copies and one "Теми занять" per block.</summary>
    public static int SheetCount(int students, int lessons, int studentsPerCopy, int lessonsPerCopy)
    {
        var groups = Groups(students, studentsPerCopy);
        var blocks = Groups(lessons, lessonsPerCopy);
        return (groups * blocks) + blocks;
    }

    public static BlankFormFill Map(
        Report report, IReportTexts texts, CultureInfo uiCulture, int studentsPerCopy, int lessonsPerCopy)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(uiCulture);
        ArgumentOutOfRangeException.ThrowIfLessThan(studentsPerCopy, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(studentsPerCopy, StudentsPerCopyMax);
        ArgumentOutOfRangeException.ThrowIfLessThan(lessonsPerCopy, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lessonsPerCopy, LessonsPerCopyMax);

        var grading = Grading(report);
        var students = grading?.Rows ?? [];
        var topics = Topics(report);

        // FR-005.1: the grading columns that are lessons, matched in order to the "Lesson topics" rows.
        var lessonColumns = grading is null
            ? []
            : Enumerable.Range(0, grading.Columns.Count).Where(i => !grading.Columns[i].IsMaterial).ToList();
        if (grading is not null && lessonColumns.Count != topics.Count)
        {
            throw new InvalidOperationException("The report's lesson columns and lesson topics differ.");
        }

        // US-060 spec FR-008.3, I-4: the header line as a note on the course cell; the printed text is unchanged.
        var notice = ReportWorkbookMapper.NoticeText(report.Header.Notice, texts, uiCulture);
        var course = WorkbookCell.OfText(ReportWorkbookMapper.CourseText(report.Header)) with { Note = notice };
        var teachers = WorkbookCell.OfText(ReportWorkbookMapper.TeachersText(report.Header, texts));
        var groups = Groups(students.Count, studentsPerCopy);
        var blocks = Groups(topics.Count, lessonsPerCopy);
        var gradingNumbered = groups * blocks > 1;
        var topicsNumbered = blocks > 1;

        // FR-006: for each block, the grading copy of every group in order, then the topics copy of the block.
        var copies = new List<BlankFormCopy>();
        var gradingNumber = 0;
        for (var block = 0; block < blocks; block++)
        {
            for (var group = 0; group < groups; group++)
            {
                var placements = new List<BlankFormPlacement> { new(BlankFormMarker.Course, 0, 0, course) };
                for (var k = 0; k < StudentRows; k++)
                {
                    var student = (group * studentsPerCopy) + k;
                    var holds = k < studentsPerCopy && student < students.Count;
                    placements.Add(new(
                        BlankFormMarker.StudentNumber, k, 0, holds ? WorkbookCell.OfNumber(student + 1) : WorkbookCell.Empty()));
                    placements.Add(new(
                        BlankFormMarker.StudentName,
                        k,
                        0,
                        holds ? ReportWorkbookMapper.StudentCell(students[student], texts, uiCulture) : WorkbookCell.Empty()));
                }

                // FR-005.2, FR-005.5: the 24th column never holds a lesson (lessons per copy ≤ 23).
                for (var j = 0; j < LessonColumns; j++)
                {
                    var lesson = (block * lessonsPerCopy) + j;
                    var holdsLesson = j < lessonsPerCopy && lesson < topics.Count;
                    placements.Add(new(
                        BlankFormMarker.LessonDate, 0, j, holdsLesson ? WorkbookCell.OfDate(topics[lesson].LessonDate) : WorkbookCell.Empty()));
                    for (var k = 0; k < StudentRows; k++)
                    {
                        var student = (group * studentsPerCopy) + k;
                        var holds = holdsLesson && k < studentsPerCopy && student < students.Count;
                        placements.Add(new(
                            BlankFormMarker.Grade,
                            k,
                            j,
                            holds
                                ? ReportWorkbookMapper.GradingCell(students[student].Cells[lessonColumns[lesson]], texts, uiCulture)
                                : WorkbookCell.Empty()));
                    }
                }

                gradingNumber++;
                copies.Add(new BlankFormCopy(
                    BlankFormSheet.Grading, gradingNumber, gradingNumbered, BlankFormFooterSection.Right, placements));
            }

            copies.Add(TopicsCopy(topics, teachers, texts, block, lessonsPerCopy, topicsNumbered));
        }

        return new BlankFormFill(copies);
    }

    /// <summary>Spec FR-005.4: the teachers and the block's lessons, numbered through the file; unused rows cleared.</summary>
    private static BlankFormCopy TopicsCopy(
        IReadOnlyList<LessonTopicRow> topics, WorkbookCell teachers, IReportTexts texts, int block, int lessonsPerCopy, bool numbered)
    {
        var placements = new List<BlankFormPlacement> { new(BlankFormMarker.Teachers, 0, 0, teachers) };
        for (var r = 0; r < TopicRows; r++)
        {
            var lesson = (block * lessonsPerCopy) + r;
            if (r < lessonsPerCopy && lesson < topics.Count)
            {
                var topic = topics[lesson];
                placements.Add(new(BlankFormMarker.TopicNumber, r, 0, WorkbookCell.OfNumber(lesson + 1)));
                placements.Add(new(BlankFormMarker.TopicDate, r, 0, WorkbookCell.OfDate(topic.LessonDate)));
                placements.Add(new(BlankFormMarker.TopicHours, r, 0, WorkbookCell.OfNumber(topic.Hours)));
                // US-060 spec FR-008.4: the item mark as a note on its topic title; the grading sheet has no title cell.
                var mark = topic.NotInClassroom ? ReportWorkbookMapper.ItemMarkText(texts) : null;
                var title = WorkbookCell.OfText(topic.Title) with { Note = mark };
                placements.Add(new(BlankFormMarker.TopicTitle, r, 0, title));
            }
            else
            {
                placements.Add(new(BlankFormMarker.TopicNumber, r, 0, WorkbookCell.Empty()));
                placements.Add(new(BlankFormMarker.TopicDate, r, 0, WorkbookCell.Empty()));
                placements.Add(new(BlankFormMarker.TopicHours, r, 0, WorkbookCell.Empty()));
                placements.Add(new(BlankFormMarker.TopicTitle, r, 0, WorkbookCell.Empty()));
            }
        }

        return new BlankFormCopy(BlankFormSheet.Topics, block + 1, numbered, BlankFormFooterSection.Left, placements);
    }

    /// <summary>Spec FR-006, I-6: max(1, ⌈count / size⌉).</summary>
    private static int Groups(int count, int size) => Math.Max(1, (count + size - 1) / size);

    private static GradingPart? Grading(Report report) => report.EmptyStateKey is null ? report.Grading : null;

    private static IReadOnlyList<LessonTopicRow> Topics(Report report) =>
        report.EmptyStateKey is null ? report.LessonTopics?.Rows ?? [] : [];
}
