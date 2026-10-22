namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// One search, as the tool hands it to a backend.
/// </summary>
/// <remarks>
/// Everything here is already resolved and bounded: the model's arguments have been merged
/// with the tool's settings and clamped to what the tool allows. What a backend still has to
/// do is translate it into its own API and say so when it cannot honour a part of it.
/// </remarks>
/// <param name="Query">What to search for.</param>
/// <param name="Language">An IETF language tag, or the any-language value when the search should not be restricted.</param>
/// <param name="TimeRange">How far back to look, or null for no restriction.</param>
/// <param name="Page">The result page, starting at 1, or null for the first page.</param>
/// <param name="SafeSearch">How strict to filter explicit results or null to leave the decision to the service.</param>
/// <param name="Limit">The most results the tool will use from this backend.</param>
/// <param name="TimeoutSeconds">How long the backend may take before the search counts as failed.</param>
public sealed record WebSearchQuery(string Query, string? Language, string? TimeRange, int? Page, SafeSearchPolicy? SafeSearch, int Limit, int TimeoutSeconds);