namespace ClassroomAgent.Application.Models;

/// <summary>The status shown in the "Last synchronization" block (US-017 API design §3).</summary>
public enum LastSynchronizationStatus
{
    NeverRun,
    Running,
    Completed,
    Failed,
}
