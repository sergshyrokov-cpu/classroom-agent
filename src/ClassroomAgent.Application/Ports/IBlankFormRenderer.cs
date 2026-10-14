using ClassroomAgent.Application.Models.Export;

namespace ClassroomAgent.Application.Ports;

/// <summary>
/// US-043 spec FR-001, FR-005: fills copies of the shipped form "Бланк для групових занять" and returns the file bytes.
/// Implemented in Infrastructure; a missing or broken embedded form is a program defect and throws (FR-003, I-2).
/// </summary>
public interface IBlankFormRenderer
{
    Task<byte[]> RenderAsync(BlankFormFill fill, CancellationToken cancellationToken);
}
