using AIStudio.Models;
using AIStudio.Tools.Security;
using AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

namespace AIStudio.Tools.RAG;

/// <summary>
/// How much room the passages of the classic RAG process may take in a request, and which of them fit into it.
/// </summary>
/// <remarks>
/// <para>
/// The classic RAG process sends its passages along with every message, in the system prompt. A
/// chunk is as long as the embedding model takes at once, by default 8,192 tokens, so the default
/// of ten passages of a single data source may already exceed what many models read, and the
/// provider then refuses the whole message. The budget keeps the passages within a share of the
/// model's context window, see For.
/// </para>
/// <para>
/// The size of a passage is that of its Markdown as the model reads it, measured after the prompt
/// injection filter: the filter may make a text longer, and the budget has to judge what the model
/// receives. Which number a passage gets depends on which passages fit, so it is measured with the
/// longest header it can get.
/// </para>
/// </remarks>
/// <param name="limit">How much the passages may take together, in the unit of measure; at least 1.</param>
/// <param name="measure">Measures a text in the unit of the budget, e.g., in tokens.</param>
public sealed class PassageBudget(int limit, Func<string, CancellationToken, Task<int>> measure)
{
    /// <summary>
    /// The share of a known context window the passages may take.
    /// </summary>
    /// <remarks>
    /// The rest of the window stays for everything else of the request: the conversation, its
    /// attachments, the system prompt, the tools, and the answer. Half of it leaves a long
    /// conversation room, while the passages still get as much as the window of most models holds
    /// of them.
    /// </remarks>
    internal const double SHARE_OF_THE_CONTEXT_WINDOW = 0.5;

    /// <summary>
    /// How many characters the passages may take when nothing is known about the context window of the model.
    /// </summary>
    /// <remarks>
    /// As many as one page of Semantic Search returns, so that both ways of searching hand the
    /// model at most the same amount of text when nothing more is known.
    /// </remarks>
    internal const int CHARACTERS_WITHOUT_A_KNOWN_CONTEXT_WINDOW = SemanticSearchTool.MAX_RESULT_CHARACTERS;

    /// <summary>
    /// How much the passages may take together, in the unit of the budget.
    /// </summary>
    public int Limit { get; } = limit >= 1 ? limit : throw new ArgumentOutOfRangeException(nameof(limit), limit, "A budget has to hold at least 1.");

    /// <summary>
    /// A budget which counts characters.
    /// </summary>
    /// <param name="limit">How many characters the passages may take together; at least 1.</param>
    /// <returns>The budget.</returns>
    public static PassageBudget InCharacters(int limit) => new(limit, (text, _) => Task.FromResult(text.Length));

    /// <summary>
    /// The budget for a model with the given context window.
    /// </summary>
    /// <remarks>
    /// A known window is counted in tokens, as the model counts it. Of an unknown one, nothing can
    /// be said, so the passages get as many characters as one page of Semantic Search.
    /// </remarks>
    /// <param name="contextWindow">The context window of the model which answers.</param>
    /// <param name="countTokensAsync">Counts the tokens of a text for that model.</param>
    /// <returns>The budget.</returns>
    public static PassageBudget For(ContextWindow contextWindow, Func<string, CancellationToken, Task<int>> countTokensAsync) => contextWindow.IsKnown
        ? new(Math.Max(1, (int)(contextWindow.DefaultTokens * SHARE_OF_THE_CONTEXT_WINDOW)), countTokensAsync)
        : InCharacters(CHARACTERS_WITHOUT_A_KNOWN_CONTEXT_WINDOW);

    /// <summary>
    /// Selects the passages which fit into the budget.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The data sources take turns, rank by rank: the best passage of each data source comes before
    /// the second best of any, so that a data source with many long passages does not crowd out the
    /// others. A passage which does not fit into what is left is skipped, and the next ones still
    /// get their chance: the classic RAG process has no further pages which could bring it later.
    /// </para>
    /// <para>
    /// A passage longer than the whole budget is shortened until it fits, and filtered again, see
    /// IRetrievalContextExtensions.ShortenToFitAsync. It then has to fit into what is left like any
    /// other passage.
    /// </para>
    /// </remarks>
    /// <param name="contextsByDataSource">The prepared passages of each data source, each in the order of their rank.</param>
    /// <param name="guardService">The prompt injection filter, for passages which have to be shortened.</param>
    /// <param name="reportingScope">The action the passages were retrieved for, or null when they were retrieved for none.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The passages which fit, grouped by data source, how many of each data source fit, and how many did not.</returns>
    public async Task<PassageSelection> SelectAsync(IReadOnlyList<IReadOnlyList<PreparedRetrievalContext>> contextsByDataSource, PromptInjectionGuardService guardService, PromptInjectionReportingScope? reportingScope = null, CancellationToken token = default)
    {
        var candidateCount = contextsByDataSource.Sum(contexts => contexts.Count);
        var mostPassages = contextsByDataSource.Select(contexts => contexts.Count).DefaultIfEmpty(0).Max();
        var selected = contextsByDataSource.Select(_ => new List<PreparedRetrievalContext>()).ToList();
        var remaining = this.Limit;
        var leftOutCount = 0;

        for (var rank = 0; rank < mostPassages; rank++)
        {
            for (var index = 0; index < contextsByDataSource.Count; index++)
            {
                if (rank >= contextsByDataSource[index].Count)
                    continue;

                // A budget which is used up holds nothing more, so there is nothing to measure:
                if (remaining is 0)
                {
                    leftOutCount++;
                    continue;
                }

                var context = contextsByDataSource[index][rank];
                var size = await this.MeasureAsync(context, candidateCount, token);
                while (size > this.Limit && await this.ShortenAsync(context, size, candidateCount, guardService, reportingScope, token) is { } shorterContext)
                {
                    context = shorterContext;
                    size = await this.MeasureAsync(context, candidateCount, token);
                }

                if (size > remaining)
                {
                    leftOutCount++;
                    continue;
                }

                selected[index].Add(context);
                remaining -= size;
            }
        }

        return new([..selected.SelectMany(contexts => contexts)], [..selected.Select(contexts => contexts.Count)], leftOutCount);
    }

    /// <summary>
    /// Measures a passage as the model reads it, in the unit of the budget.
    /// </summary>
    private Task<int> MeasureAsync(PreparedRetrievalContext context, int candidateCount, CancellationToken token) => measure(WithLongestHeader(context, candidateCount), token);

    /// <summary>
    /// Shortens a passage which is longer than the whole budget by about as much as it exceeds it.
    /// </summary>
    /// <remarks>
    /// The budget may count something else than characters, e.g., tokens, while a passage is
    /// shortened by characters. Both grow roughly in proportion, so the passage keeps the share of
    /// its characters which the budget holds of its size. Where the proportion is off, the caller
    /// measures again and shortens once more.
    /// </remarks>
    /// <param name="context">The passage.</param>
    /// <param name="size">The size of the passage in the unit of the budget, more than its limit.</param>
    /// <param name="candidateCount">How many passages there are to select from.</param>
    /// <param name="guardService">The prompt injection filter.</param>
    /// <param name="reportingScope">The action the passage was retrieved for, or null.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The shorter passage, or null when nothing of it is left to cut.</returns>
    private async Task<PreparedRetrievalContext?> ShortenAsync(PreparedRetrievalContext context, int size, int candidateCount, PromptInjectionGuardService guardService, PromptInjectionReportingScope? reportingScope, CancellationToken token)
    {
        var characters = WithLongestHeader(context, candidateCount).Length;
        var maxCharacters = (int)((long)characters * this.Limit / size);
        var shorterContext = await context.ShortenToFitAsync(maxCharacters, shorter => WithLongestHeader(shorter, candidateCount).Length, guardService, reportingScope, token);
        return WithLongestHeader(shorterContext, candidateCount).Length < characters ? shorterContext : null;
    }

    /// <summary>
    /// Writes a passage with the longest header it can get: neither its number nor the count of the passages the model receives exceeds the count of the passages to select from.
    /// </summary>
    private static string WithLongestHeader(PreparedRetrievalContext context, int candidateCount) => context.ToMarkdown(candidateCount, candidateCount);
}