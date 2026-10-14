namespace ClassroomAgent.Application.Models.Export;

/// <summary>
/// US-043 spec FR-006, FR-006.1: one filled copy of a sheet of the form. Its name is the form's sheet name, followed by
/// a space and <see cref="Number"/> when <see cref="IsNumbered"/> (a kind with one copy keeps the bare name, I-7); the
/// same number, as digits, is the text of its <see cref="FooterSection"/>.
/// </summary>
public sealed record BlankFormCopy(
    BlankFormSheet Sheet,
    int Number,
    bool IsNumbered,
    BlankFormFooterSection FooterSection,
    IReadOnlyList<BlankFormPlacement> Placements);
