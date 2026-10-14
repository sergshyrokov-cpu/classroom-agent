namespace ClassroomAgent.Application.Models.Export;

/// <summary>US-043 spec FR-004, VR-001: how an exported journal is laid out; absent is <see cref="Program"/>.</summary>
public enum ExportLayout
{
    /// <summary>US-028: the program's own layout.</summary>
    Program,

    /// <summary>US-043: the shipped form "Бланк для групових занять", filled copy by copy.</summary>
    GroupLessonsBlank,
}
