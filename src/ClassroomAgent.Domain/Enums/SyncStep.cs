namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// The step of a synchronization run that stopped it (US-031 spec FR-010, I-5): the Classroom read or the Meet events
/// read. Stored as <c>classroom</c> / <c>meet</c> (db-design §4).
/// </summary>
public enum SyncStep
{
    Classroom,
    Meet,

    /// <summary>US-032 spec FR-006: the automatic linking of meeting codes after the Meet step.</summary>
    Linking,
}
