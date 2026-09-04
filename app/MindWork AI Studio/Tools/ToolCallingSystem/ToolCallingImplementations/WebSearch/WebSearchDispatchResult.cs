namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// The outcome of one search, after however many services were asked for it.
/// </summary>
/// <remarks>
/// The same thing a single backend returns, once the tool no longer knows how many of them
/// were involved. A search where no service answered at all is not this: it is thrown, because
/// there is nothing to report about it besides the reasons.
/// </remarks>
/// <param name="Backends">Which services answered, in the order they were asked.</param>
/// <param name="Candidates">The hits of all of them, merged by URL and renumbered.</param>
/// <param name="CandidateCount">How many hits the services returned in total, before equivalent URLs were merged.</param>
/// <param name="Notes">What the tool should report about this search besides its hits, such as a service that could not be asked or a part of the query one of them could not honour.</param>
internal sealed record WebSearchDispatchResult(IReadOnlyList<WebSearchBackend> Backends, IReadOnlyList<SearchCandidate> Candidates, int CandidateCount, IReadOnlyList<string> Notes);