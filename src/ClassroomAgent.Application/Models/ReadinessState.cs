namespace ClassroomAgent.Application.Models;

/// <summary>Readiness of the installation (US-005 spec FR-013; DC-11).</summary>
public enum ReadinessState
{
    Healthy,
    Degraded,
    Unhealthy,
}
