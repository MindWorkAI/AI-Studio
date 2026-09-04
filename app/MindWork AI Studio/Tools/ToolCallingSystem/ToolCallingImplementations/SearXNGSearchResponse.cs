namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

/// <param name="Candidates">The search hits, already deduplicated and limited.</param>
/// <param name="CandidateCount">How many hits the instance returned within the requested limit.</param>
/// <param name="UnresponsiveEngines">The engines that did not answer, each with its reason when the instance gave one.</param>
internal sealed record SearXNGSearchResponse(IReadOnlyList<SearchCandidate> Candidates, int CandidateCount, IReadOnlyList<string> UnresponsiveEngines);