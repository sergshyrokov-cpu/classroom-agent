using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace ClassroomAgent.Tests.Architecture;

/// <summary>
/// US-040 AC-004, spec FR-003, SC-14: caching is decided by one host-wide rule, never by a mark on a controller, an
/// action or a page model. The behaviour of the rule is proven by the <c>NoStoreResponseTests</c> of each host; this
/// guards the other half — that no per-page caching decision exists beside it.
/// </summary>
public sealed class NoStoreRuleTests
{
    public static TheoryData<string> Hosts => new("ClassroomAgent.Web", "ClassroomAgent.ControlPlane");

    [Theory]
    [MemberData(nameof(Hosts))]
    public void NoTypeOrMember_CarriesAResponseCacheAttribute(string host)
    {
        var assembly = host == "ClassroomAgent.Web" ? typeof(ClassroomAgent.Web.Program).Assembly : typeof(Program).Assembly;

        var marked = assembly.GetTypes()
            .SelectMany(t => new MemberInfo[] { t }.Concat(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)))
            .Where(m => m.IsDefined(typeof(ResponseCacheAttribute), inherit: false))
            .Select(m => m is Type t ? t.FullName : $"{m.DeclaringType?.FullName}.{m.Name}")
            .ToList();

        Assert.Empty(marked);
    }
}
