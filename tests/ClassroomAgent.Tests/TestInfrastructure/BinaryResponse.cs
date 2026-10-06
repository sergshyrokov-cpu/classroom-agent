using System.Net;
using System.Text;
using System.Text.Json;

namespace ClassroomAgent.Tests.TestInfrastructure;

/// <summary>US-028: a response whose body is kept as bytes — the exported file, or the API-6 JSON error body.</summary>
public sealed record BinaryResponse(
    HttpStatusCode Status,
    string? Location,
    byte[] Bytes,
    IReadOnlyList<KeyValuePair<string, string>> Headers)
{
    public string Text => Encoding.UTF8.GetString(Bytes);

    /// <summary>The first value of a header, case-insensitive; null when absent.</summary>
    public string? Header(string name) =>
        Headers.Where(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase)).Select(h => h.Value).FirstOrDefault();

    /// <summary>The API-6 body as a JSON object (api-conventions.md API-6).</summary>
    public JsonElement Json()
    {
        using var document = JsonDocument.Parse(Bytes);
        return document.RootElement.Clone();
    }
}
