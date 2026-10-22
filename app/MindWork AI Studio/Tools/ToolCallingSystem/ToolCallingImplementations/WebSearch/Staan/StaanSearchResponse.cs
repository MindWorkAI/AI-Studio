namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch.Staan;

/// <summary>
/// What Staan answers to a search.
/// </summary>
/// <remarks>
/// Declared down to what the tool reads. Staan also returns an identifier for the search and
/// echoes the market, count, and offset it used; none of that reaches the user or the model,
/// and a field nothing reads only raises the question of what it is for.
/// </remarks>
internal sealed record StaanSearchResponse
{
    public StaanQueryInfo? Query { get; init; }

    public StaanWebSection? Web { get; init; }
}