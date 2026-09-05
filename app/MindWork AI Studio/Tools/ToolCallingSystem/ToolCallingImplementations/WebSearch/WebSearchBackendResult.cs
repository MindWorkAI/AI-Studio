namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <param name="Backend">Which backend answered.</param>
/// <param name="Candidates">The search hits, already deduplicated and limited.</param>
/// <param name="CandidateCount">How many hits the backend returned within the requested limit, before equivalent URLs were merged. It is therefore at least as large as the candidate list.</param>
/// <param name="Notes">What the tool should report about this search besides its hits, such as engines that did not answer or a part of the query the backend could not honour.</param>
public sealed record WebSearchBackendResult(WebSearchBackend Backend, IReadOnlyList<SearchCandidate> Candidates, int CandidateCount, IReadOnlyList<string> Notes);