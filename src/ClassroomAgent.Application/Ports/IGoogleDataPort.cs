namespace ClassroomAgent.Application.Ports;

/// <summary>
/// Marks a port whose implementation calls a Google API (US-007 spec FR-007). A use case holding one must
/// also take <c>IReadOnlyModeGuard</c> and call it first, so that in read-only mode no call to Google is
/// made at all (SC-5, SC-8). The marker carries no member and no Google SDK type (AD-4).
/// </summary>
public interface IGoogleDataPort;
