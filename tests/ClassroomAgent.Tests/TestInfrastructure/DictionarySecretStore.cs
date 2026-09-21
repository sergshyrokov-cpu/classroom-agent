using ClassroomAgent.Application.Ports;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>A secret store over an in-memory dictionary, so a test never touches the process environment.</summary>
public sealed class DictionarySecretStore(IReadOnlyDictionary<string, string> secrets) : ISecretStore
{
    public string? Resolve(string reference) =>
        secrets.TryGetValue(reference, out var value) ? value : null;
}
