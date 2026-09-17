namespace ClassroomAgent.Application.Models;

/// <summary>Why a call to the Control Plane gave no usable answer (US-005 api-design §6, §11).</summary>
public enum CheckFailureCategory
{
    Unreachable,
    Timeout,
    ErrorAnswer,
    UnparseableAnswer,
    UnknownInstallation,
}
