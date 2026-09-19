using System.Reflection;
using ClassroomAgent.Application.Ports;
using ClassroomAgent.Application.UseCases;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>
/// The rule US-007 AC-007 makes unbypassable, as a pure function over a set of types so that it can be
/// proven to detect a violation (against synthetic types) before it is run over the real
/// <c>ClassroomAgent.Application</c> assembly (spec FR-010).
///
/// A <b>protected path</b> is a use case whose constructor takes <see cref="IUnitOfWork"/> (it writes) or a
/// parameter marked <see cref="IGoogleDataPort"/> (it reaches Google). Each must either take
/// <see cref="IReadOnlyModeGuard"/> in the same constructor or be registered in
/// <see cref="PermittedServiceWrites"/> as one of the BR-026 closed list.
/// </summary>
public static class WritePathRule
{
    /// <summary>One message per offending type; empty when the rule holds.</summary>
    public static IReadOnlyList<string> Violations(
        IEnumerable<Type> types,
        IReadOnlyDictionary<Type, PermittedServiceWrite> declarations) =>
        types
            .Where(IsProtectedPath)
            .Where(t => !TakesTheGuard(t) && !declarations.ContainsKey(t))
            .Select(Message)
            .ToList();

    /// <summary>Whether the rule applies to this type at all.</summary>
    public static bool IsProtectedPath(Type type)
    {
        // The enforcement point itself decorates IUnitOfWork; it is the rule, not a subject of it.
        if (typeof(IUnitOfWork).IsAssignableFrom(type))
        {
            return false;
        }

        return Parameters(type).Any(p => p.ParameterType == typeof(IUnitOfWork)
            || typeof(IGoogleDataPort).IsAssignableFrom(p.ParameterType));
    }

    public static bool TakesTheGuard(Type type) =>
        Parameters(type).Any(p => p.ParameterType == typeof(IReadOnlyModeGuard));

    /// <summary>What the author of a later Story is told to do, instead of deleting the test.</summary>
    public static string Message(Type type) =>
        $"{type.FullName} writes or reaches Google without passing the read-only enforcement point. "
        + $"Take {nameof(IReadOnlyModeGuard)} in its constructor and call EnsureAllowedAsync first; "
        + $"or, only if the write is on the BR-026 closed list, register the type in "
        + $"{nameof(PermittedServiceWrites)} with the {nameof(PermittedServiceWrite)} member it performs. "
        + "A new service write is permitted only by extending that list in trebovaniya.md section 2 first.";

    /// <summary>The candidate types of the production Application layer.</summary>
    public static IReadOnlyList<Type> ApplicationUseCases() =>
        typeof(GetLegitimacyModeQuery).Assembly
            .GetExportedTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.Namespace == typeof(GetLegitimacyModeQuery).Namespace)
            .ToList();

    private static IEnumerable<ParameterInfo> Parameters(Type type) =>
        type.GetConstructors().SelectMany(c => c.GetParameters());
}
