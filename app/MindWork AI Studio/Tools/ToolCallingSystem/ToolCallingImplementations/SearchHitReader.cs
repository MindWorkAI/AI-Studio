using System.Diagnostics;
using System.Text.Json.Nodes;

using AIStudio.Tools.Security;
using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

/// <summary>
/// Reads the best hits of a search, such as one of the wiki, so the model gets their content
/// with the search instead of one call of Read Web Page per hit.
/// </summary>
/// <remarks>
/// A chat may call ToolSelectionRules.MAX_TOOL_CALLS tools per answer. A search followed by four pages costs five of them, and a
/// question which needs a few searches used them all up before the model could answer. Web search
/// reads its hits itself for the same reason, see WebSearchResultRetrievalService.<br/><br/>
/// Only hits within the searched site are read, under the rules of the search itself: the caller
/// builds the options of every request, which keep it and its redirects within the site and send
/// the operating system's sign-in to the site only. Documents follow the settings of Read Web Page,
/// since they are the ones that tool would read; a hit within the site is internal, so the default
/// reads them. The hits share one character budget, so a search of several hits does not grow
/// larger than an ordinary page. The budget is shared once all hits are read, among those which
/// were read: a hit which failed or is shorter than its share leaves the rest to the others. A
/// document is therefore read up to the whole budget, which keeps it far below its size limit.<br/><br/>
/// All hits together may take a given time at most, so slow documents cannot hold up the chat for
/// several rounds of the timeout. A hit which is not read by then fails like any other.<br/><br/>
/// Every hit is filtered for prompt injections, the pages in one request and the documents while
/// the runtime reads them, all within the action of the search, so the user hears of one search
/// rather than of every page. A hit which cannot be read costs that hit only, whatever went wrong:
/// the model learns why and may still open it with Read Web Page.
/// </remarks>
public sealed class SearchHitReader(WebPageRetrievalService webPageRetrievalService, PromptInjectionGuardService promptInjectionGuardService, ToolSettingsService toolSettingsService, ILogger<SearchHitReader> logger)
{
    // The defaults and the budget limit of web search, so a search of the wiki brings about as much as one of the web:
    public const int DEFAULT_HITS_TO_READ = 5;
    public const int MAX_HITS_TO_READ = 10;
    public const int DEFAULT_CONTENT_CHARACTERS = 100_000;
    public const int MAX_CONTENT_CHARACTERS = 200_000;

    // As many as web search reads at once:
    private const int MAX_PARALLEL_READS = 4;

    private const string OUT_OF_TIME = "The time for reading the results of the search ran out before this one was read.";

    /// <summary>
    /// Reads the given hits, each to its share of the character budget.
    /// </summary>
    /// <param name="hits">The hits to read, in the order of their relevance. All must lie within the searched site.</param>
    /// <param name="maxContentCharacters">The character budget of all hits together.</param>
    /// <param name="createOptions">Builds the options of the request for one hit, given the document options to pass on. They keep the request within the site.</param>
    /// <param name="maxDuration">How long reading all hits may take, or null for no limit beyond the timeout of each request.</param>
    /// <param name="reportingScope">The action of the search, or null to report what the filter removed right away.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>What came of each hit, in the order of the hits.</returns>
    public async Task<IReadOnlyList<SearchHitContent>> ReadAsync(IReadOnlyList<Uri> hits, int maxContentCharacters, Func<Uri, WebDocumentOptions, WebPageRetrievalOptions> createOptions, TimeSpan? maxDuration = null, PromptInjectionReportingScope? reportingScope = null, CancellationToken token = default)
    {
        if (hits.Count is 0 || maxContentCharacters <= 0)
            return [];

        var (documentAccess, maxDocumentBytes) = await ReadWebPageTool.ReadDocumentSettingsAsync(toolSettingsService);
        var documents = new WebDocumentOptions
        {
            // A hit lies within the site, so it is internal; the request cannot leave the site:
            IsAllowed = (_, _) => ReadWebPageTool.IsDocumentAllowed(documentAccess, isInternal: true),
            MaxBytes = maxDocumentBytes,

            // The whole budget, since a document may get it all when the other hits fail:
            MaxCharacters = maxContentCharacters,
            ReportingScope = reportingScope,
        };

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (maxDuration is { } duration)
            deadline.CancelAfter(duration);

        using var semaphore = new SemaphoreSlim(MAX_PARALLEL_READS);
        var reads = await Task.WhenAll(hits.Select((hit, index) => this.ReadHitAsync(hit, index, createOptions(hit, documents), semaphore, deadline.Token, token)));
        reads = ShareBudget(reads, maxContentCharacters);

        //
        // One request for all hits that were read, so the user hears of the search once, grouped
        // by page, rather than once per hit. It comes after the cut, so only what the model gets
        // is filtered:
        //
        var read = reads.Where(hit => hit.Content is not null).ToList();
        var sanitized = await WebPageContentSanitizer.SanitizeAsync(
            promptInjectionGuardService,
            read.Select(hit => (hit.Content!, PromptInjectionSource.WebContent(hit.FinalUrl.AbsoluteUri))).ToList(),
            reportingScope);

        var sanitizedByIndex = read.Select((hit, position) => (hit.Index, Content: sanitized[position])).ToDictionary(entry => entry.Index, entry => entry.Content);
        return reads.Select(hit => sanitizedByIndex.TryGetValue(hit.Index, out var content)
                ? new SearchHitContent(hit.Url, hit.FinalUrl, content.Title, content.Markdown, hit.IsPartial, null)
                : new SearchHitContent(hit.Url, hit.FinalUrl, string.Empty, string.Empty, false, hit.Failure))
            .ToList();
    }

    /// <summary>
    /// Shares the budget among the hits which were read and cuts each one to its share.
    /// </summary>
    /// <remarks>
    /// The shortest hits come first: one shorter than an equal share keeps all of it and leaves the
    /// rest to the longer ones. A hit which failed takes nothing.
    /// </remarks>
    /// <param name="reads">What came of each hit, in the order of the hits.</param>
    /// <param name="maxContentCharacters">The character budget of all hits together.</param>
    /// <returns>The hits, in the same order, each within its share.</returns>
    private static HitRead[] ShareBudget(HitRead[] reads, int maxContentCharacters)
    {
        var shared = reads.ToArray();
        var readHits = shared.Where(hit => hit.Content is not null).OrderBy(hit => hit.Content!.Markdown.Length).ToList();
        var remainingBudget = maxContentCharacters;
        var remainingHits = readHits.Count;
        foreach (var hit in readHits)
        {
            var share = Math.Max(1, remainingBudget / remainingHits);
            remainingHits--;

            var content = hit.Content!;
            if (content.Markdown.Length <= share)
            {
                remainingBudget -= content.Markdown.Length;
                continue;
            }

            var markdown = MarkdownTruncator.Truncate(content.Markdown, share);
            remainingBudget -= markdown.Length;
            shared[Array.IndexOf(shared, hit)] = hit with { Content = content with { Markdown = markdown }, IsPartial = true };
        }

        return shared;
    }

    private async Task<HitRead> ReadHitAsync(Uri hit, int index, WebPageRetrievalOptions options, SemaphoreSlim semaphore, CancellationToken deadlineToken, CancellationToken token)
    {
        try
        {
            await semaphore.WaitAsync(deadlineToken);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            logger.LogInformation("Did not read a search hit, because the time for reading the hits ran out. Index={Index}", index);
            return new HitRead(index, hit, hit, null, false, OUT_OF_TIME);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var retrieved = await webPageRetrievalService.RetrieveAsync(hit, options, deadlineToken);
            var isPartial = retrieved.IsCutAtCharacterLimit || retrieved.HasUnreadableParts;
            logger.LogInformation("Read a search hit. Index={Index}, ContentKind={ContentKind}, ReadCharacters={ReadCharacters}, IsPartial={IsPartial}, DurationMs={DurationMs}", index, retrieved.ContentKind, retrieved.ExtractedPage.Markdown.Length, isPartial, stopwatch.ElapsedMilliseconds);
            return new HitRead(index, hit, retrieved.Page.FinalUrl, WebPageModelContent.From(retrieved.ExtractedPage, retrieved.ExtractedPage.Markdown), isPartial, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (deadlineToken.IsCancellationRequested)
        {
            logger.LogInformation("Stopped reading a search hit, because the time for reading the hits ran out. Index={Index}, DurationMs={DurationMs}", index, stopwatch.ElapsedMilliseconds);
            return new HitRead(index, hit, hit, null, false, OUT_OF_TIME);
        }
        // Anything else as well, such as a temporary file the data directory refuses: the search was read, and one hit must not cost it:
        catch (Exception exception)
        {
            logger.LogInformation("Could not read a search hit. Index={Index}, Reason={Reason}, DurationMs={DurationMs}", index, exception.GetType().Name, stopwatch.ElapsedMilliseconds);
            return new HitRead(index, hit, hit, null, false, GetFailure(exception));
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Why a hit could not be read, as the model learns it.
    /// </summary>
    /// <remarks>
    /// A refused redirect names the address it would have led to. That address reaches the model
    /// nowhere else, and once it stands in a tool result, it counts as one given in the chat, which
    /// Read Web Page may open even while the free address choice is off. Its own text therefore
    /// names none, as Read Web Page words it for the same refusal.<br/><br/>
    /// Only the failures of reading a page or a document tell their own reason. Any other, such as
    /// one of the file system, would name the paths of this computer to the model.
    /// </remarks>
    internal static string GetFailure(Exception exception) => exception switch
    {
        WebPageAccessBlockedException { Reason: WebPageAccessBlockReason.TARGET_NOT_ALLOWED } => "The site redirected this hit to an address outside of it, which is not followed.",
        WebPageAccessBlockedException or InvalidOperationException or TimeoutException or HttpRequestException => exception.Message,
        _ => "The hit could not be read.",
    };

    /// <summary>
    /// What the model gets of a hit next to its excerpt.
    /// </summary>
    /// <param name="target">The JSON object of the hit.</param>
    /// <param name="content">What came of reading it.</param>
    public static void AddTo(JsonObject target, SearchHitContent content)
    {
        if (content.Failure is not null)
        {
            target["content_error"] = $"{content.Failure} Read the hit with `read_web_page` if it is still needed.";
            return;
        }

        target["content_status"] = content.IsPartial ? "partial" : "complete";
        target["text_content"] = content.Markdown;
    }

    private sealed record HitRead(int Index, Uri Url, Uri FinalUrl, WebPageModelContent? Content, bool IsPartial, string? Failure);
}

/// <summary>
/// What came of reading one hit of a search.
/// </summary>
/// <param name="Url">The address of the hit.</param>
/// <param name="FinalUrl">Where it was read, after every redirect.</param>
/// <param name="Title">The title of the page or document as read, filtered.</param>
/// <param name="Markdown">The content, filtered and cut to its share. Empty when it could not be read.</param>
/// <param name="IsPartial">Whether the content was cut or misses parts.</param>
/// <param name="Failure">Why the hit could not be read, or null when it was.</param>
public sealed record SearchHitContent(Uri Url, Uri FinalUrl, string Title, string Markdown, bool IsPartial, string? Failure);