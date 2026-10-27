namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-060 spec FR-003.1, VR-002: the stored states of the marks course that choose its header line.</summary>
public enum CourseHeaderVariant
{
    /// <summary>No mark, no skip record: no line.</summary>
    None,

    /// <summary>AC-006: marked "not in Classroom" since D1 → "Course not in Classroom since D1".</summary>
    NotInClassroom,

    /// <summary>AC-007: marked "not visible" since D2, last read R1 → "Data not updated since R1".</summary>
    NotVisible,

    /// <summary>AC-007: a skip record, last read R2 → "Data not updated since R2".</summary>
    Skipped,

    /// <summary>AC-011, OD-002 a: a skip record and no last complete read → "Data not updated" with no date.</summary>
    SkippedNeverRead,

    /// <summary>AC-012, I-2: "not in Classroom" since D1 and a skip record → only "Course not in Classroom since D1".</summary>
    NotInClassroomAndSkipped,

    /// <summary>AC-012, FR-003.3: an unchanged archived course with no skip record → no line.</summary>
    ArchivedUnchanged,
}
