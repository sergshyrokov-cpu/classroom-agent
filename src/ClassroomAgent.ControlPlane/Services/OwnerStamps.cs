using System.Security.Cryptography;

namespace ClassroomAgent.ControlPlane.Services;

/// <summary>Random security and concurrency stamps for the Owner account.</summary>
public static class OwnerStamps
{
    public static string New() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
}
