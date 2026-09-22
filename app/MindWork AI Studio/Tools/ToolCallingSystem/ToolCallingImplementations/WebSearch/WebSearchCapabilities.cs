namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// What one search service can do with the parts of a search besides the query itself.
/// </summary>
/// <remarks>
/// Two different answers come out of this, and what separates them is who asked for the thing
/// the service cannot do. The safe search policy is the user's, and an organization can lock
/// it — so a service that cannot filter is not asked at all, because searching unfiltered
/// would work around a decision somebody made on purpose. The language and the time range come
/// from the model, which can read a note and search again, so a service that cannot honour
/// them is still asked and reports what it did instead.<br/><br/>
/// The result page is the exception among the model's own arguments: page 1 handed over as
/// page 3 would be hits the model already read, with nothing in the answer to say so, and no
/// note can undo that. A service that does not reach the requested page is therefore left out
/// like one that cannot filter.
/// </remarks>
/// <param name="SupportsSafeSearch">Whether the service filters explicit results on request.</param>
/// <param name="SupportsTimeRange">Whether the service can restrict a search to a recent period of time.</param>
/// <param name="SupportsLanguage">Whether the service can restrict a search to one language.</param>
/// <param name="MaxPage">The highest result page the service serves.</param>
public sealed record WebSearchCapabilities(bool SupportsSafeSearch, bool SupportsTimeRange, bool SupportsLanguage, int MaxPage);