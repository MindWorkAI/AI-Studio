using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.WebSearch;

/// <summary>
/// Decides which of the configured search services answer one search, and merges what they
/// returned into a single ranked list.
/// </summary>
/// <remarks>
/// It owns the search backends as well, in the order of the backend enum, because that order
/// is part of what it decides: a failover walks the services in it. Ordering them here rather
/// than taking them as the dependency injection container happened to hand them over is what
/// makes a search repeatable.<br/><br/>
/// One service failing is not the search failing. Whichever strategy is running, a failure is
/// kept as a note and the remaining services are still asked; only a search that no service
/// answered is thrown, and then with every reason collected. The exception is the user
/// cancelling: that ends the search at once, because nobody is waiting for its result any more.
/// </remarks>
internal sealed class WebSearchDispatcher(IEnumerable<IWebSearchBackend> backends)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(WebSearchDispatcher).Namespace, nameof(WebSearchDispatcher));

    public IReadOnlyList<IWebSearchBackend> Backends { get; } = backends.OrderBy(backend => backend.Backend).ToList();

    /// <summary>
    /// The services the user filled in enough of to be asked.
    /// </summary>
    public IReadOnlyList<IWebSearchBackend> GetConfiguredBackends(IReadOnlyDictionary<string, string> settingsValues) => this.Backends.Where(backend => backend.IsConfigured(settingsValues)).ToList();

    public int CountConfiguredBackends(IReadOnlyDictionary<string, string> settingsValues) => this.Backends.Count(backend => backend.IsConfigured(settingsValues));

    public async Task<WebSearchDispatchResult> SearchAsync(WebSearchBackendStrategy strategy, WebSearchBackend? primaryBackend, WebSearchQuery query, IReadOnlyDictionary<string, string> settingsValues, CancellationToken token = default)
    {
        var notes = new List<string>();
        var backendsToAsk = this.ResolveBackendsToAsk(strategy, primaryBackend, query, settingsValues, notes);
        var outcomes = strategy is WebSearchBackendStrategy.PARALLEL
            ? await SearchInParallelAsync(backendsToAsk, query, settingsValues, token)
            : await SearchOneAfterAnotherAsync(backendsToAsk, query, settingsValues, token);

        AppendBackendNotes(notes, outcomes);
        var backendResults = outcomes.Select(outcome => outcome.Result).OfType<WebSearchBackendResult>().ToList();

        //
        // Nothing to report and nothing to search with: the notes hold every reason, so they
        // travel in the message rather than in a result nobody will get:
        //
        if (backendResults.Count is 0)
            throw new InvalidOperationException($"{TB("None of the configured search services could be asked.")} {string.Join(" ", notes)}");

        return new WebSearchDispatchResult(
            backendResults.Select(result => result.Backend).ToList(),
            MergeCandidates(backendResults, query.Limit),
            backendResults.Sum(result => result.CandidateCount),
            notes);
    }

    /// <summary>
    /// Which services to ask, in which order.
    /// </summary>
    /// <remarks>
    /// A stored choice that no longer fits what is configured does not stop the search: it is
    /// reported as a note and the search runs with what is there. Both meta settings are hidden
    /// while fewer than two services are configured, so such a value can outlive the situation
    /// it was made for, and a search refusing to run over one would leave the user with nothing
    /// they can act on.
    /// </remarks>
    private IReadOnlyList<IWebSearchBackend> ResolveBackendsToAsk(WebSearchBackendStrategy strategy, WebSearchBackend? primaryBackend, WebSearchQuery query, IReadOnlyDictionary<string, string> settingsValues, List<string> notes)
    {
        var configuredBackends = this.GetConfiguredBackends(settingsValues);
        if (configuredBackends.Count is 0)
            throw new InvalidOperationException(TB("No search service is configured for the web search."));

        //
        // Only the strategies that ask one service before the others have a use for the chosen
        // one. Asking all of them at once has none, which is also why the dialog hides the
        // choice then:
        //
        var usesChosenBackend = strategy is WebSearchBackendStrategy.FAILOVER or WebSearchBackendStrategy.SPECIFIC;
        var chosenBackend = usesChosenBackend ? configuredBackends.FirstOrDefault(backend => backend.Backend == primaryBackend) : null;
        if (usesChosenBackend && chosenBackend is null && configuredBackends.Count > 1)
        {
            if (primaryBackend is not null)
                notes.Add($"The chosen search service {primaryBackend.Value.ToName()} is not configured, so the configured services were asked one after another instead.");
            else if (strategy is WebSearchBackendStrategy.SPECIFIC)
                notes.Add("No search service is chosen, so the configured services were asked one after another instead.");
        }

        List<IWebSearchBackend> backendsToAsk;
        if (strategy is WebSearchBackendStrategy.SPECIFIC && chosenBackend is not null)
            backendsToAsk = [chosenBackend];
        else if (chosenBackend is null)
            backendsToAsk = [..configuredBackends];
        else
            backendsToAsk = [chosenBackend, ..configuredBackends.Where(backend => backend != chosenBackend)];

        return RemoveBackendsWithoutThisPage(backendsToAsk, query, notes);
    }

    /// <summary>
    /// Drops the services that cannot serve the requested result page.
    /// </summary>
    /// <remarks>
    /// Answering page 1 where page 3 was asked for would look right and be wrong: the model
    /// would read the same hits a second time without any way to notice. Leaving the service
    /// out is the honest answer, and the note says which one dropped out.
    /// </remarks>
    private static IReadOnlyList<IWebSearchBackend> RemoveBackendsWithoutThisPage(IReadOnlyList<IWebSearchBackend> backendsToAsk, WebSearchQuery query, List<string> notes)
    {
        if (query.Page is null or <= 1)
            return backendsToAsk;

        var remainingBackends = backendsToAsk.Where(backend => query.Page <= backend.MaxPage).ToList();
        if (remainingBackends.Count is 0)
            throw new ArgumentException($"Argument 'page' must be less than or equal to {backendsToAsk.Max(backend => backend.MaxPage)}.");

        foreach (var backend in backendsToAsk.Where(backend => query.Page > backend.MaxPage))
            notes.Add($"{backend.Backend.ToName()} was not asked, because it does not serve result page {query.Page}.");

        return remainingBackends;
    }

    /// <remarks>
    /// The first service that returns a hit ends the search; everything after it is there for
    /// the case that the ones before it answered nothing. Each of them gets the full search
    /// timeout, so a search across three unreachable services takes three times as long as one
    /// — that is the price of a failover, and the reason the timeout is a setting.
    /// </remarks>
    private static async Task<IReadOnlyList<BackendOutcome>> SearchOneAfterAnotherAsync(IReadOnlyList<IWebSearchBackend> backendsToAsk, WebSearchQuery query, IReadOnlyDictionary<string, string> settingsValues, CancellationToken token)
    {
        var outcomes = new List<BackendOutcome>();
        foreach (var backend in backendsToAsk)
        {
            var outcome = await SearchOneAsync(backend, query, settingsValues, token);
            outcomes.Add(outcome);
            if (outcome.Result is { Candidates.Count: > 0 })
                break;
        }

        return outcomes;
    }

    /// <remarks>
    /// Every service is asked, and every service costs a request of whatever it grants for
    /// free. That is what the user chose this strategy for: two indexes see different parts of
    /// the web, and a hit both of them found is a stronger hit than one only one of them had.
    /// </remarks>
    private static async Task<IReadOnlyList<BackendOutcome>> SearchInParallelAsync(IReadOnlyList<IWebSearchBackend> backendsToAsk, WebSearchQuery query, IReadOnlyDictionary<string, string> settingsValues, CancellationToken token) =>
        await Task.WhenAll(backendsToAsk.Select(backend => SearchOneAsync(backend, query, settingsValues, token)));

    /// <remarks>
    /// Everything a service can go wrong with is caught here, not just the failures its own
    /// client words: a backend is free to throw whatever describes its situation, and one of
    /// them throwing must not take the search down with it. The user cancelling is the one
    /// thing that does, which is why the filter asks the token rather than the exception type.
    /// </remarks>
    private static async Task<BackendOutcome> SearchOneAsync(IWebSearchBackend backend, WebSearchQuery query, IReadOnlyDictionary<string, string> settingsValues, CancellationToken token)
    {
        try
        {
            return new(backend, await backend.SearchAsync(query, settingsValues, token), null);
        }
        catch (Exception exception) when (!token.IsCancellationRequested)
        {
            return new(backend, null, exception.Message);
        }
    }

    /// <summary>
    /// Collects what the services reported besides their hits.
    /// </summary>
    /// <remarks>
    /// A note says which service it came from as soon as more than one was asked, and does not
    /// while only one was: a search through a single service has nobody to be confused with,
    /// and its notes already name it where that matters.
    /// </remarks>
    private static void AppendBackendNotes(List<string> notes, IReadOnlyList<BackendOutcome> outcomes)
    {
        var attributesNotes = outcomes.Count > 1;
        foreach (var outcome in outcomes)
        {
            var backendName = outcome.Backend.Backend.ToName();
            var result = outcome.Result;
            if (result is null)
            {
                notes.Add($"{backendName} could not be asked: {outcome.Error}");
                continue;
            }

            if (attributesNotes && result.Candidates.Count is 0)
                notes.Add($"{backendName} returned no hits.");

            foreach (var note in result.Notes)
                notes.Add(attributesNotes ? $"{backendName}: {note}" : note);
        }
    }

    /// <summary>
    /// Merges the hits of several services into one ranked list.
    /// </summary>
    /// <remarks>
    /// The services are read in step: the first hit of each of them, then the second hit of
    /// each, and so on. Their own scores cannot be compared — every engine computes a different
    /// number and none of them is published — so the position each service gave a hit is all
    /// there is to go by, and giving each service the same say at every position is the only
    /// merge that does not quietly favour one of them.<br/><br/>
    /// The same page found by two services becomes one candidate that names both, and the
    /// limit applies to that merged list rather than to each service, so the tool retrieves as
    /// many pages as it would for a single service.
    /// </remarks>
    private static IReadOnlyList<SearchCandidate> MergeCandidates(IReadOnlyList<WebSearchBackendResult> backendResults, int limit)
    {
        // One service needs no merging, and its candidates are limited and ranked already:
        if (backendResults.Count is 1)
            return backendResults[0].Candidates;

        var candidatesByUrl = new Dictionary<string, SearchCandidate>(StringComparer.Ordinal);
        var mergedCandidates = new List<SearchCandidate>();
        var mostCandidatesOfOneBackend = backendResults.Max(result => result.Candidates.Count);
        for (var position = 0; position < mostCandidatesOfOneBackend; position++)
        {
            foreach (var backendResult in backendResults)
            {
                if (position >= backendResult.Candidates.Count)
                    continue;

                var candidate = backendResult.Candidates[position];
                var normalizedUrl = SearchCandidate.NormalizeUrl(candidate.RetrievalUrl);
                if (candidatesByUrl.TryGetValue(normalizedUrl, out var existingCandidate))
                {
                    existingCandidate.Merge(candidate);
                    continue;
                }

                // Cloned, because merging writes to the candidate, and the result a backend
                // handed over is not ours to change:
                var mergedCandidate = candidate.Clone();
                candidatesByUrl[normalizedUrl] = mergedCandidate;
                mergedCandidates.Add(mergedCandidate);
            }
        }

        //
        // The ranks the services gave are gone at this point, and the merged order is what
        // replaces them. Renumbering says so, and keeps the ranks the tool reports a plain
        // 1, 2, 3 rather than a mix of two services' numbering:
        //
        var limitedCandidates = mergedCandidates.Take(limit).ToList();
        for (var index = 0; index < limitedCandidates.Count; index++)
            limitedCandidates[index].Rank = index + 1;

        return limitedCandidates;
    }

    /// <param name="Backend">The service that was asked.</param>
    /// <param name="Result">What it answered, or null when it could not be asked.</param>
    /// <param name="Error">Why it could not be asked, or null when it answered.</param>
    private sealed record BackendOutcome(IWebSearchBackend Backend, WebSearchBackendResult? Result, string? Error);
}