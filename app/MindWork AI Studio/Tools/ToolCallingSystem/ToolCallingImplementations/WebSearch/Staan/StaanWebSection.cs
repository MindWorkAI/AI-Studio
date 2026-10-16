namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;

/// <summary>
/// The web hits of a Staan search.
/// </summary>
internal sealed record StaanWebSection
{
    public IReadOnlyList<StaanSearchResult> Results { get; init; } = [];
}