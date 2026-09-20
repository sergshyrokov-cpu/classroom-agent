namespace ClassroomAgent.Domain.Enums;

/// <summary>
/// The interface language of an account (NFR-073). Declared here rather than shared with
/// <c>ControlPlane.Persistence.UiLanguage</c>: <c>ControlPlane</c> may not reference <c>Domain</c>
/// (<c>package-map.md</c>), so the duplication is required by the dependency rules (entity model §2.3).
/// </summary>
public enum UiLanguage
{
    Uk,
    En,
}
