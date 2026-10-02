using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Databases;
using AIStudio.Tools.Databases.IndexStore;
using AIStudio.Tools.Databases.VectorStore;
using AIStudio.Tools.Mail;
using AIStudio.Tools.RAG;

namespace AIStudio.Tools.Services;

/// <summary>
/// Searches, counts and reads the mails of the mailboxes, for the mail tools.
/// </summary>
/// <remarks>
/// Every way into a mailbox goes through here, and each one checks on its own whether the provider
/// of the chat may read the mailbox. The tools offer only the mailboxes it may read, but the settings
/// may change between preparing a request and running one of its calls.<br/><br/>
/// Everything comes from the index, nothing from the server, so nothing here signs in anywhere. The
/// log names a mailbox, but never a subject, an address, or a folder.
/// </remarks>
public sealed class MailboxRetrievalService(SettingsManager settingsManager, DatabaseClientProvider databaseClientProvider, LocalIndexSearchService indexSearch, ILogger<MailboxRetrievalService> logger)
{
    /// <summary>
    /// How many chunks each channel delivers for every mail a page needs.
    /// </summary>
    /// <remarks>
    /// The channels find chunks, a page lists mails. A mail which matches tends to match with several
    /// of its chunks, its header block and its text, maybe an attachment as well, so a window of as
    /// many chunks as mails would come up short. Paging stops at RetrievalPaging.MAX_RESULT_WINDOW
    /// mails, which makes four hundred chunks per channel at most.
    /// </remarks>
    private const int CHUNKS_PER_MAIL = 4;

    /// <summary>
    /// The mailboxes which the provider of a chat may read, in the order the tools offer them.
    /// </summary>
    /// <remarks>
    /// The same mailboxes always come in the same order, by their number and then by their id: the
    /// providers cache a request from its beginning, and the tools describing the mailboxes are part
    /// of it, cf. SemanticSearchTool.InOfferOrder.
    /// </remarks>
    /// <param name="chatProviderConfidence">How much the provider of the chat is trusted.</param>
    /// <returns>The mailboxes, none while one of the previews is switched off.</returns>
    public IReadOnlyList<DataSourceMailbox> GetReadableMailboxes(ConfidenceLevel chatProviderConfidence)
    {
        if (!PreviewFeatures.PRE_RAG_2024.IsEnabled(settingsManager) || !PreviewFeatures.PRE_MAILBOXES_2026.IsEnabled(settingsManager))
            return [];

        return settingsManager.ConfigurationData.Mailboxes
            .Where(mailbox => IsReadable(mailbox.ConfidenceLevel, chatProviderConfidence, this.GetEmbeddingProviderConfidence(mailbox)))
            .OrderBy(mailbox => mailbox.Num)
            .ThenBy(mailbox => mailbox.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Whether the providers which see the content of a mailbox may see it.
    /// </summary>
    /// <remarks>
    /// Two providers see it while it is searched: the provider of the chat reads what is found, and
    /// the embedding provider gets the query, which the model may well have written from a mail. The
    /// embedding provider has to meet the level even though it embedded the mails long before: its
    /// confidence may have been lowered since. A mailbox whose embedding provider is gone cannot be
    /// read at all, because nobody can tell whether a replacement would meet the level.
    /// </remarks>
    /// <param name="mailboxConfidence">The confidence level the mailbox requires.</param>
    /// <param name="chatProviderConfidence">How much the provider of the chat is trusted.</param>
    /// <param name="embeddingProviderConfidence">How much the embedding provider of the mailbox is trusted, or null when it is not available.</param>
    /// <returns>True when the mailbox may be read.</returns>
    internal static bool IsReadable(ConfidenceLevel mailboxConfidence, ConfidenceLevel chatProviderConfidence, ConfidenceLevel? embeddingProviderConfidence) =>
        embeddingProviderConfidence is { } embeddingConfidence
        && chatProviderConfidence.AllowsMailboxConfidenceLevel(mailboxConfidence)
        && embeddingConfidence.AllowsMailboxConfidenceLevel(mailboxConfidence);

    /// <summary>
    /// Reads how far the index of a mailbox reaches.
    /// </summary>
    /// <param name="chatProviderConfidence">How much the provider of the chat is trusted.</param>
    /// <param name="mailboxId">The mailbox.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The coverage, or null when the index cannot be read right now.</returns>
    /// <exception cref="MailboxNotReadableException">The mailbox is not configured, or the provider of the chat may not read it.</exception>
    public async Task<MailboxCoverage?> GetCoverageAsync(ConfidenceLevel chatProviderConfidence, string mailboxId, CancellationToken token)
    {
        var mailbox = this.RequireReadableMailbox(mailboxId, chatProviderConfidence);
        try
        {
            var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
            if (!indexStore.IsAvailable)
            {
                logger.LogWarning("Cannot tell how far the index of mailbox '{MailboxName}' ({MailboxId}) reaches, because local RAG index '{DatabaseName}' is unavailable.", mailbox.Name, mailbox.Id, indexStore.Name);
                return null;
            }

            return await ReadCoverageAsync(indexStore, mailbox, DateTimeOffset.UtcNow, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Cannot tell how far the index of mailbox '{MailboxName}' ({MailboxId}) reaches.", mailbox.Name, mailbox.Id);
            return null;
        }
    }

    /// <param name="indexStore">The index store, which has to be available.</param>
    /// <param name="mailbox">The mailbox.</param>
    /// <param name="now">The point in time the period of the mailbox is counted back from.</param>
    /// <param name="token">The cancellation token.</param>
    internal static async Task<MailboxCoverage> ReadCoverageAsync(IndexStoreClient indexStore, DataSourceMailbox mailbox, DateTimeOffset now, CancellationToken token)
    {
        var syncState = await indexStore.GetMailboxSyncStateAsync(mailbox.Id, token);
        var refusedSignIn = await indexStore.GetMailboxAuthFailureAsync(mailbox.Id, token);
        var folders = await indexStore.GetMailFoldersAsync(mailbox.Id, token);

        return new(mailbox.MaxAge.GetReceivedSince(now), syncState.LastSyncCompletedUtc, refusedSignIn?.FailedAtUtc, syncState.PendingRemovalCount, folders);
    }

    /// <summary>
    /// Searches one page of the mails of a mailbox which meet the conditions.
    /// </summary>
    /// <remarks>
    /// With a query, the mails are searched by meaning and by words, and each mail comes with the
    /// passage which matched best. Without one, the mails which meet the conditions are listed, the
    /// most recently received first. Either way, a page holds the mails of the page of each channel
    /// and is cut the same way, see RetrievalPaging. The query comes from the model, so its problems
    /// are reported to the model and not to the user.
    /// </remarks>
    /// <param name="chatProviderConfidence">How much the provider of the chat is trusted.</param>
    /// <param name="mailboxId">The mailbox.</param>
    /// <param name="query">What to search for, or null to list the mails.</param>
    /// <param name="filter">The conditions the mails have to meet.</param>
    /// <param name="page">The page, starting at 1, up to RetrievalPaging.GetLastPage for the page size of the mailbox.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The page; empty with a gap when the mailbox could not be searched.</returns>
    /// <exception cref="MailboxNotReadableException">The mailbox is not configured, or the provider of the chat may not read it.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page is below 1 or beyond the last page.</exception>
    public async Task<MailSearchPage> SearchAsync(ConfidenceLevel chatProviderConfidence, string mailboxId, string? query, MailFilter filter, int page, CancellationToken token)
    {
        var mailbox = this.RequireReadableMailbox(mailboxId, chatProviderConfidence);
        var pageSize = (int)mailbox.MaxMatches;

        // Checks the page before anything is searched:
        _ = RetrievalPaging.GetWindowSize(page, pageSize);
        if (pageSize == 0)
            return MailSearchPage.EMPTY;

        var run = new RetrievalRun(queryWrittenByUser: false);
        if (await indexSearch.IsAwaitingReindexAsync(mailbox, run, token))
            return MailSearchPage.EMPTY with { Gaps = run.GetGaps() };

        var byRelevance = !string.IsNullOrWhiteSpace(query);
        try
        {
            var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
            if (!indexStore.IsAvailable)
            {
                logger.LogWarning("Skipping the search of mailbox '{MailboxName}' ({MailboxId}) because local RAG index '{DatabaseName}' is unavailable.", mailbox.Name, mailbox.Id, indexStore.Name);
                run.Add(RetrievalGap.NOT_SEARCHED);
                return MailSearchPage.EMPTY with { Gaps = run.GetGaps() };
            }

            var result = string.IsNullOrWhiteSpace(query)
                ? await ListNewestFirstAsync(indexStore, mailbox.Id, filter, page, pageSize, token)
                : await this.SearchByRelevanceAsync(indexStore, mailbox, query, filter, page, pageSize, run, token);

            var gaps = run.GetGaps();
            logger.LogInformation(
                "Searched mailbox '{MailboxName}' ({MailboxId}). ByRelevance={ByRelevance}, Page={Page}, Mails={MailCount}, HasMore={HasMore}, Gaps=[{Gaps}].",
                mailbox.Name,
                mailbox.Id,
                byRelevance,
                page,
                result.Hits.Count,
                result.HasMore,
                string.Join(", ", gaps));

            return result with { Gaps = gaps };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Searching mailbox '{MailboxName}' ({MailboxId}) failed. ByRelevance={ByRelevance}.", mailbox.Name, mailbox.Id, byRelevance);
            run.Add(RetrievalGap.NOT_SEARCHED);
            return MailSearchPage.EMPTY with { Gaps = run.GetGaps() };
        }
    }

    /// <summary>
    /// Lists one page of the mails which meet the conditions, the most recently received first.
    /// </summary>
    /// <remarks>
    /// The window of the page is fetched from the start, as with a search by relevance, although
    /// the index could skip to the page directly. That keeps one rule for how far a mailbox can be
    /// paged through and when a further page is worth asking for, whichever way it is searched.
    /// </remarks>
    /// <param name="indexStore">The index store, which has to be available.</param>
    /// <param name="mailboxId">The mailbox.</param>
    /// <param name="filter">The conditions.</param>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">How many mails a page lists.</param>
    /// <param name="token">The cancellation token.</param>
    internal static async Task<MailSearchPage> ListNewestFirstAsync(IndexStoreClient indexStore, string mailboxId, MailFilter filter, int page, int pageSize, CancellationToken token)
    {
        var window = await indexStore.QueryMailsAsync(mailboxId, filter, 0, RetrievalPaging.GetWindowSize(page, pageSize), token);
        var (mailIds, hasMore) = RetrievalPaging.Cut(window, page, pageSize);
        var summaries = await indexStore.GetMailSummariesAsync(mailboxId, mailIds, token);

        return new(summaries.Select(summary => new MailSearchHit(summary, null)).ToList(), hasMore, []);
    }

    private async Task<MailSearchPage> SearchByRelevanceAsync(IndexStoreClient indexStore, DataSourceMailbox mailbox, string query, MailFilter filter, int page, int pageSize, RetrievalRun run, CancellationToken token)
    {
        var chunkWindow = RetrievalPaging.GetWindowSize(page, pageSize) * CHUNKS_PER_MAIL;

        //
        // Without a condition, every chunk of the collection may match, and the collection holds
        // nothing but this mailbox. Sending the ids of all its chunks along would only make the
        // request large, so the whole collection is searched, and the summaries leave out the few
        // mails which lost their last place on the server since the last sync. With a condition,
        // the vector search sees the chunks of the mails which meet it, and nothing else:
        //
        var vectorFilter = filter.HasConditions ? new VectorSearchFilter(await indexStore.GetMailChunkIdsAsync(mailbox.Id, filter, token)) : null;
        var vectorTask = indexSearch.SearchVectorsAsync(mailbox, query, chunkWindow, vectorFilter, run, token);
        var keywordTask = indexSearch.SearchKeywordsAsync(mailbox, chunkWindow, store => store.SearchMailChunksAsync(mailbox.Id, query, filter, chunkWindow, token), run, token);

        await Task.WhenAll(vectorTask, keywordTask);
        token.ThrowIfCancellationRequested();

        var vectorChunks = vectorTask.Result;
        var keywordChunks = keywordTask.Result;
        var (passages, hasMore) = RetrievalPaging.Merge(
            BestPassagePerMail(vectorChunks.Select(chunk => new MailPassage(chunk.ParentFileId, chunk.Text))),
            BestPassagePerMail(keywordChunks.Select(chunk => new MailPassage(chunk.ParentFileId, chunk.ChunkText))),
            passage => passage.MailId,
            page,
            pageSize);

        hasMore = hasMore || MayHoldMoreMails(page, pageSize, chunkWindow, vectorChunks.Count, keywordChunks.Count);

        var passageTexts = passages
            .DistinctBy(passage => passage.MailId, StringComparer.Ordinal)
            .ToDictionary(passage => passage.MailId, passage => passage.Text, StringComparer.Ordinal);

        var summaries = await indexStore.GetMailSummariesAsync(mailbox.Id, passages.Select(passage => passage.MailId).ToList(), token);
        return new(summaries.Select(summary => new MailSearchHit(summary, passageTexts[summary.MailId])).ToList(), hasMore, []);
    }

    /// <summary>
    /// Keeps the best chunk of every mail, which turns a list of chunks into a list of mails.
    /// </summary>
    /// <param name="chunks">The chunks a channel found, the best first.</param>
    /// <returns>One passage per mail, in the order of their best chunks.</returns>
    internal static IReadOnlyList<MailPassage> BestPassagePerMail(IEnumerable<MailPassage> chunks)
    {
        var seenMails = new HashSet<string>(StringComparer.Ordinal);
        return chunks.Where(chunk => !string.IsNullOrWhiteSpace(chunk.MailId) && seenMails.Add(chunk.MailId)).ToList();
    }

    /// <summary>
    /// Whether a channel may hold further mails which its window of chunks did not reach.
    /// </summary>
    /// <remarks>
    /// A channel which filled its whole window may have more to show, even when its chunks belong
    /// to fewer mails than the page needed. Saying there is more when there is not costs one page
    /// which turns out empty; saying the opposite would hide mails.
    /// </remarks>
    /// <param name="page">The page, starting at 1.</param>
    /// <param name="pageSize">How many mails a page lists per channel.</param>
    /// <param name="chunkWindow">How many chunks each channel was asked for.</param>
    /// <param name="chunkCounts">How many chunks each channel delivered.</param>
    /// <returns>True when the next page is worth asking for.</returns>
    internal static bool MayHoldMoreMails(int page, int pageSize, int chunkWindow, params int[] chunkCounts) =>
        page < RetrievalPaging.GetLastPage(pageSize) && chunkCounts.Any(count => count >= chunkWindow);

    /// <summary>
    /// Counts the mails of a mailbox which meet the conditions.
    /// </summary>
    /// <param name="chatProviderConfidence">How much the provider of the chat is trusted.</param>
    /// <param name="mailboxId">The mailbox.</param>
    /// <param name="filter">The conditions.</param>
    /// <param name="grouping">How to break the number down.</param>
    /// <param name="maxGroups">How many groups to return at most, the largest ones.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The count; without a number and with a gap when the mailbox could not be counted.</returns>
    /// <exception cref="MailboxNotReadableException">The mailbox is not configured, or the provider of the chat may not read it.</exception>
    public async Task<MailCountOutcome> CountAsync(ConfidenceLevel chatProviderConfidence, string mailboxId, MailFilter filter, MailCountGrouping grouping, int maxGroups, CancellationToken token)
    {
        var mailbox = this.RequireReadableMailbox(mailboxId, chatProviderConfidence);

        // While the index is built anew, it holds only part of the mails, and any number would be too low:
        var run = new RetrievalRun(queryWrittenByUser: false);
        if (await indexSearch.IsAwaitingReindexAsync(mailbox, run, token))
            return new(null, run.GetGaps());

        try
        {
            var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
            if (!indexStore.IsAvailable)
            {
                logger.LogWarning("Skipping the count of mailbox '{MailboxName}' ({MailboxId}) because local RAG index '{DatabaseName}' is unavailable.", mailbox.Name, mailbox.Id, indexStore.Name);
                return new(null, [RetrievalGap.NOT_SEARCHED]);
            }

            var count = await indexStore.CountMailsAsync(mailbox.Id, filter, grouping, maxGroups, token);
            logger.LogInformation("Counted mailbox '{MailboxName}' ({MailboxId}). Grouping={Grouping}, Total={TotalCount}, Groups={GroupCount}.", mailbox.Name, mailbox.Id, grouping, count.TotalCount, count.Groups.Count);
            return new(count, []);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Counting mailbox '{MailboxName}' ({MailboxId}) failed. Grouping={Grouping}.", mailbox.Name, mailbox.Id, grouping);
            return new(null, [RetrievalGap.NOT_SEARCHED]);
        }
    }

    /// <summary>
    /// Reads a mail from whichever mailbox holds it, among those the provider of the chat may read.
    /// </summary>
    /// <remarks>
    /// The id of a mail is derived from the id of its mailbox, so no two mailboxes share one. A mail
    /// of a mailbox the provider may not read is not found, exactly like a mail nobody ever indexed.
    /// </remarks>
    /// <param name="chatProviderConfidence">How much the provider of the chat is trusted.</param>
    /// <param name="mailId">The id of the mail.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The mail, or null when no mailbox the provider may read holds it.</returns>
    /// <exception cref="InvalidOperationException">The index cannot be read right now, so nobody can tell whether the mail exists.</exception>
    public async Task<MailReading?> ReadAsync(ConfidenceLevel chatProviderConfidence, string mailId, CancellationToken token)
    {
        var mailboxes = this.GetReadableMailboxes(chatProviderConfidence);
        if (mailboxes.Count == 0)
            return null;

        var indexStore = await databaseClientProvider.GetIndexStoreAsync(token);
        if (!indexStore.IsAvailable)
            throw new InvalidOperationException($"No mail can be read, because local RAG index '{indexStore.Name}' is unavailable.");

        return await ReadMailAsync(indexStore, mailboxes, mailId, token);
    }

    /// <remarks>
    /// A mail which lost its last place on the server is not read: it is gone, or about to turn up
    /// under another id once the next sync found where it went.
    /// </remarks>
    /// <param name="indexStore">The index store, which has to be available.</param>
    /// <param name="mailboxes">The mailboxes to look in.</param>
    /// <param name="mailId">The id of the mail.</param>
    /// <param name="token">The cancellation token.</param>
    internal static async Task<MailReading?> ReadMailAsync(IndexStoreClient indexStore, IReadOnlyList<DataSourceMailbox> mailboxes, string mailId, CancellationToken token)
    {
        foreach (var mailbox in mailboxes)
        {
            var summaries = await indexStore.GetMailSummariesAsync(mailbox.Id, [mailId], token);
            if (summaries.Count == 0)
                continue;

            var mail = await indexStore.GetMailAsync(mailbox.Id, mailId, token);
            if (mail is null)
                continue;

            var inReplyToMailId = await indexStore.FindMailByMessageIdAsync(mailbox.Id, mail.InReplyTo, token);
            return new(mailbox, summaries[0], mail, inReplyToMailId == mailId ? null : inReplyToMailId);
        }

        return null;
    }

    private DataSourceMailbox RequireReadableMailbox(string mailboxId, ConfidenceLevel chatProviderConfidence)
    {
        foreach (var mailbox in this.GetReadableMailboxes(chatProviderConfidence))
            if (mailbox.Id == mailboxId)
                return mailbox;

        logger.LogWarning("The mailbox '{MailboxId}' is not configured, or the provider of the chat may not read it. Its mails stay closed.", mailboxId);
        throw new MailboxNotReadableException(mailboxId);
    }

    private ConfidenceLevel? GetEmbeddingProviderConfidence(DataSourceMailbox mailbox) =>
        DataSourceEmbeddingProviders.TryResolve(settingsManager, mailbox, out var embeddingProvider) ? embeddingProvider.GetConfidenceLevel(settingsManager) : null;
}