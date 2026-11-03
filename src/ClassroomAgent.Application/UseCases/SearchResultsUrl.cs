namespace ClassroomAgent.Application.UseCases;

/// <summary>
/// Openapi US-071 <c>x-return-path</c>: the results page's address, the language switcher's return path. Only a validated
/// text reaches it (spec section 7).
/// </summary>
public static class SearchResultsUrl
{
    /// <summary>The page's path; equal to the host's route constant.</summary>
    public const string Path = "/workspace/search";

    /// <summary>The path with <c>q</c>, encoded; the bare path with no text.</summary>
    public static string Build(string? searchText) =>
        string.IsNullOrEmpty(searchText) ? Path : Path + "?q=" + Uri.EscapeDataString(searchText);
}
