using ClassroomAgent.Domain.Enums;

namespace ClassroomAgent.Application.Models;

/// <summary>
/// The school-wide defaults an account inherits at creation (US-008 spec FR-001, FR-011, FR-017). Today that is
/// the interface language, taken from installation configuration and Ukrainian when unset (NFR-073, VR-004).
/// Choosing a personal language is US-039.
/// </summary>
public sealed record SchoolDefaults(UiLanguage UiLanguage);
