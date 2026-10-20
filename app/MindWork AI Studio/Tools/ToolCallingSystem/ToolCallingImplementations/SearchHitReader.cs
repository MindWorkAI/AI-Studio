using System.Diagnostics;
using System.Text.Json.Nodes;

using AIStudio.Tools.Security;
using AIStudio.Tools.Web;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations;

/// <summary>
/// Reads the best hits of a search of the wiki or the intranet, so the model gets their content
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
/// larger than an ordinary page. Each hit is cut to its share, a document while it is read.<br/><br/>
/// Every hit is filtered for prompt injections, all of them in one request, so the user hears of
/// one search rather than of every page. A hit which cannot be read costs that hit only: the model
/// learns why and may still open it with Read Web Page.
/// </remarks>
public sealed class SearchHitReader(WebPageRetrievalService webPageRetrievalService, PromptInjectionGuardService promptInjectionGuardService, ToolSettingsService toolSettingsService, ILogger<SearchHitReader> logger)
{
    public const int DEFAULT_HITS_TO_READ = 3;
    public const int MAX_HITS_TO_READ = 10;
    public const int DEFAULT_CONTENT_CHARACTERS = 30_000;
    public const int MAX_CONTENT_CHARACTERS = 100_000;

    // As many as web search reads at once:
    private const int MAX_PARALLEL_READS = 4;

    /// <summary>
    /// Reads the given hits, each to its share of the character budget.
    /// </summary>
    /// <param name="hits">The hits to read, in the order of their relevance. All must lie within the searched site.</param>
    /// <param name="maxContentCharacters">The character budget of all hits together.</param>
    /// <param name="createOptions">Builds the options of the request for one hit, given the document options to pass on. They keep the request within the site.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>What came of each hit, in the order of the hits.</returns>
    public async Task<IReadOnlyList<SearchHitContent>> ReadAsync(IReadOnlyList<Uri> hits, int maxContentCharacters, Func<Uri, WebDocumentOptions, WebPageRetrievalOptions> createOptions, CancellationToken token = default)
    {
        if (hits.Count is 0 || maxContentCharacters <= 0)
            return [];

        var share = Math.Max(1, maxContentCharacters / hits.Count);
        var (documentAccess, maxDocumentBytes) = await ReadWebPageTool.ReadDocumentSettingsAsync(toolSettingsService);
        var documents = new WebDocumentOptions
        {
            // A hit lies within the site, so it is internal; the request cannot leave the site:
            IsAllowed = (_, _) => ReadWebPageTool.IsDocumentAllowed(documentAccess, isInternal: true),
            MaxBytes = maxDocumentBytes,
            MaxCharacters = share,
        };

        using var semaphore = new SemaphoreSlim(MAX_PARALLEL_READS);
        var reads = await Task.WhenAll(hits.Select((hit, index) => this.ReadHitAsync(hit, index, share, createOptions(hit, documents), semaphore, token)));

        //
        // One request for all hits that were read, so the user hears of the search once, grouped
        // by page, rather than once per hit:
        //
        var read = reads.Where(hit => hit.Content is not null).ToList();
        var sanitized = await WebPageContentSanitizer.SanitizeAsync(
            promptInjectionGuardService,
            read.Select(hit => (hit.Content!, PromptInjectionSource.WebContent(hit.FinalUrl.AbsoluteUri))).ToList());

        var sanitizedByIndex = read.Select((hit, position) => (hit.Index, Content: sanitized[position])).ToDictionary(entry => entry.Index, entry => entry.Content);
        return reads.Select(hit => sanitizedByIndex.TryGetValue(hit.Index, out var content)
                ? new SearchHitContent(hit.Url, hit.FinalUrl, content.Title, content.Markdown, hit.IsPartial, null)
                : new SearchHitContent(hit.Url, hit.FinalUrl, string.Empty, string.Empty, false, hit.Failure))
            .ToList();
    }

    private async Task<HitRead> ReadHitAsync(Uri hit, int index, int share, WebPageRetrievalOptions options, SemaphoreSlim semaphore, CancellationToken token)
    {
        await semaphore.WaitAsync(token);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var retrieved = await webPageRetrievalService.RetrieveAsync(hit, options, token);
            var markdown = retrieved.ExtractedPage.Markdown;
            var isPartial = retrieved.IsCutAtCharacterLimit || retrieved.HasUnreadableParts;
            if (markdown.Length > share)
            {
                markdown = MarkdownTruncator.Truncate(markdown, share);
                isPartial = true;
            }

            logger.LogInformation("Read a search hit. Index={Index}, ContentKind={ContentKind}, ReturnedCharacters={ReturnedCharacters}, IsPartial={IsPartial}, DurationMs={DurationMs}", index, retrieved.ContentKind, markdown.Length, isPartial, stopwatch.ElapsedMilliseconds);
            return new HitRead(index, hit, retrieved.Page.FinalUrl, WebPageModelContent.From(retrieved.ExtractedPage, markdown), isPartial, null);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is WebPageAccessBlockedException or InvalidOperationException or TimeoutException or HttpRequestException or IOException)
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
    /// names none, as Read Web Page words it for the same refusal.
    /// </remarks>
    internal static string GetFailure(Exception exception) => exception switch
    {
        WebPageAccessBlockedException { Reason: WebPageAccessBlockReason.TARGET_NOT_ALLOWED } => "The site redirected this hit to an address outside of it, which is not followed.",
        _ => exception.Message,
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