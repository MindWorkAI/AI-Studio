using System.Text;

using AIStudio.Tools.Security;

namespace AIStudio.Tools.RAG;

public static class IRetrievalContextExtensions
{
    private static readonly ILogger<IRetrievalContext> LOGGER = Program.LOGGER_FACTORY.CreateLogger<IRetrievalContext>();

    /// <summary>
    /// Writes what the AI is told about a retrieval context, before its content follows.
    /// </summary>
    /// <remarks>
    /// The location is what lets the AI say where an answer comes from. Naming only the file is
    /// not enough in a document of two hundred pages, and we know the page: it travels from the
    /// runtime through the index into the context, together with the page a passage ends on when it
    /// goes on beyond its first one. A document without pages names its sheet and rows, or its
    /// lines, the same way. A slide has none of them, and then nothing is claimed rather than
    /// something made up.
    /// </remarks>
    /// <param name="contextBuilder">The builder to write into.</param>
    /// <param name="retrievalContext">The context to describe.</param>
    internal static void AppendContextDescription(StringBuilder contextBuilder, IRetrievalContext retrievalContext)
    {
        contextBuilder.AppendLine($"Data source name: {retrievalContext.DataSourceName}");
        contextBuilder.AppendLine($"Content category: {retrievalContext.Category}");
        if(!string.IsNullOrWhiteSpace(retrievalContext.SourceCategory))
            contextBuilder.AppendLine($"Source category: {retrievalContext.SourceCategory.ReplaceLineEndings(" ").Trim()}");

        contextBuilder.AppendLine($"Content type: {retrievalContext.Type}");
        contextBuilder.AppendLine($"Content path: {retrievalContext.Path}");

        if(retrievalContext is RetrievalTextContext textContext && DescribeLocation(textContext.PageNumber, textContext.EndPageNumber, textContext.SheetName, textContext.StartRow, textContext.EndRow, textContext.StartLine, textContext.EndLine) is { } location)
            contextBuilder.AppendLine($"Content location: {location}");

        if(retrievalContext.Links.Count is 0)
            return;

        contextBuilder.AppendLine("Additional links:");
        foreach(var link in retrievalContext.Links)
            contextBuilder.AppendLine($"- {link}");
    }

    /// <summary>
    /// Says where a passage is, the way the AI is told, e.g., "pages 12 to 14", "sheet "Budget", rows 3 to 9" or "lines 40 to 85".
    /// </summary>
    /// <remarks>
    /// In English, like everything else the AI is told about a passage. Pages come first, then
    /// rows, then lines: a passage names one kind only. An end only names a range when it lies
    /// beyond the start.
    /// </remarks>
    /// <param name="pageNumber">The page the passage starts on.</param>
    /// <param name="endPageNumber">The page it ends on.</param>
    /// <param name="sheetName">The sheet its rows are of, or null for a table without sheets.</param>
    /// <param name="startRow">The row it starts at.</param>
    /// <param name="endRow">The row it ends at.</param>
    /// <param name="startLine">The line it starts at.</param>
    /// <param name="endLine">The line it ends at.</param>
    /// <returns>Where the passage is, or null when it names nothing.</returns>
    internal static string? DescribeLocation(int? pageNumber, int? endPageNumber, string? sheetName, int? startRow, int? endRow, int? startLine, int? endLine)
    {
        if (pageNumber is > 0)
            return DescribeRange("page", "pages", pageNumber.Value, endPageNumber);

        if (startRow is > 0)
        {
            var rows = DescribeRange("row", "rows", startRow.Value, endRow);
            return string.IsNullOrEmpty(sheetName) ? rows : $"sheet \"{sheetName}\", {rows}";
        }

        return startLine is > 0 ? DescribeRange("line", "lines", startLine.Value, endLine) : null;
    }

    private static string DescribeRange(string one, string several, int start, int? end) => end > start ? $"{several} {start} to {end}" : $"{one} {start}";

    /// <summary>
    /// Writes what the AI is told about an image which matched, in place of the image itself.
    /// </summary>
    /// <remarks>
    /// The image is not passed on. Written as base64 text, it is nothing a model can look at, and
    /// a single image would take more room than all other passages together: Semantic Search
    /// left it out for its size, and the classic RAG process sent it along with every message.
    /// The description above still tells where the image is, so the model can point the user to it.
    /// </remarks>
    /// <param name="contextBuilder">The builder to write into, after the description of the context.</param>
    internal static void AppendImageMatch(StringBuilder contextBuilder)
    {
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Matched image content: an image matched here, but it is not shown to you. Do not guess what it shows. When it matters for the answer, tell the user that an image matched, and where to find it, using the path and the links above.");
    }

    /// <summary>
    /// Prepares retrieval contexts for the AI: filters them for prompt injections, writes out what
    /// the AI is told about each, and keeps what the user is shown of it.
    /// </summary>
    /// <remarks>
    /// A data source is as untrusted as any other external content: it may serve text written to
    /// steer the model rather than to answer the query. So everything a data source wrote passes
    /// the filter: the description of each context, its matched text, and each part of its
    /// surrounding content. What the app writes around them, such as the labels and the fences,
    /// does not need to.<br/><br/>
    /// All contexts go to the runtime in one request, which also gives the user one report for all
    /// of them. The passages are made from the filtered texts, so the user reads what the AI read.
    /// </remarks>
    /// <param name="retrievalContexts">The contexts to prepare.</param>
    /// <param name="guardService">The prompt injection filter.</param>
    /// <param name="reportingScope">The action the contexts are retrieved for, or null when they are retrieved for none.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>One prepared context per context, in the order they came in.</returns>
    public static async Task<IReadOnlyList<PreparedRetrievalContext>> PrepareForModelAsync(this IReadOnlyList<IRetrievalContext> retrievalContexts, PromptInjectionGuardService guardService, PromptInjectionReportingScope? reportingScope = null, CancellationToken token = default)
    {
        if (retrievalContexts.Count is 0)
            return [];

        //
        // The texts of all contexts in one list, each context with its description first, then its
        // matched text, then its surrounding content. The answer comes back in the same order, and
        // is read back below in the same way:
        //
        var texts = new List<PromptInjectionText>();
        foreach (var retrievalContext in retrievalContexts)
        {
            var source = PromptInjectionSource.RetrievalContext(retrievalContext.DataSourceName, retrievalContext.Path);
            var description = new StringBuilder();
            AppendContextDescription(description, retrievalContext);
            texts.Add(new(description.ToString(), source));

            if (retrievalContext is not RetrievalTextContext textContext)
                continue;

            texts.Add(new(textContext.MatchedText, source));
            foreach (var surrounding in textContext.SurroundingContent)
                texts.Add(new(surrounding, source));
        }

        token.ThrowIfCancellationRequested();
        var sanitizedTexts = await guardService.SanitizeAsync(texts, reportingScope);

        var preparedContexts = new List<PreparedRetrievalContext>(retrievalContexts.Count);
        var nextText = 0;
        foreach (var retrievalContext in retrievalContexts)
        {
            var body = new StringBuilder();
            body.Append(sanitizedTexts[nextText++]);

            var matchedText = string.Empty;
            IReadOnlyList<string> surroundingContent = [];
            switch (retrievalContext)
            {
                case RetrievalTextContext textContext:
                    matchedText = sanitizedTexts[nextText++];
                    surroundingContent = sanitizedTexts.Skip(nextText).Take(textContext.SurroundingContent.Count).ToList();
                    nextText += surroundingContent.Count;
                    AppendTextMatch(body, matchedText, surroundingContent);
                    break;

                case RetrievalImageContext:
                    AppendImageMatch(body);
                    break;

                default:
                    LOGGER.LogWarning($"The retrieval content type '{retrievalContext.Type}' of data source '{retrievalContext.DataSourceName}' at location '{retrievalContext.Path}' is not supported yet.");
                    break;
            }

            var sources = retrievalContext.ToSources();
            preparedContexts.Add(new(retrievalContext, body.ToString(), sources, CreatePassage(retrievalContext, sources, matchedText, surroundingContent)));
        }

        return preparedContexts;
    }

    /// <summary>
    /// Writes the content of a text context, after its description.
    /// </summary>
    /// <param name="contextBuilder">The builder to write into.</param>
    /// <param name="matchedText">The matched text, filtered.</param>
    /// <param name="surroundingContent">The surrounding content, filtered.</param>
    private static void AppendTextMatch(StringBuilder contextBuilder, string matchedText, IReadOnlyList<string> surroundingContent)
    {
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Matched text content:");
        contextBuilder.AppendLine("````");
        contextBuilder.AppendLine(matchedText);
        contextBuilder.AppendLine("````");

        if (surroundingContent.Count is 0)
            return;

        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Surrounding text content:");
        foreach (var surrounding in surroundingContent)
        {
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("````");
            contextBuilder.AppendLine(surrounding);
            contextBuilder.AppendLine("````");
        }
    }

    /// <summary>
    /// Keeps what the user is shown of a context the AI received.
    /// </summary>
    /// <remarks>
    /// The name and the path stay as the data source delivered them: they tell the user where the
    /// passage is, and the AI read them in the filtered description anyway.
    /// </remarks>
    /// <param name="retrievalContext">The context.</param>
    /// <param name="sources">The sources of the context.</param>
    /// <param name="matchedText">The matched text, filtered; empty for anything but text.</param>
    /// <param name="surroundingContent">The surrounding content, filtered.</param>
    /// <returns>The passage, or null when the context has no source.</returns>
    private static SourcePassage? CreatePassage(IRetrievalContext retrievalContext, IReadOnlyList<Source> sources, string matchedText, IReadOnlyList<string> surroundingContent)
    {
        //
        // The user finds a passage under the source it belongs to. A context without a source, i.e.,
        // one which names neither a document nor its data source, is listed nowhere, so its passage
        // could never be shown and would only take up room:
        //
        if (sources.Count is 0)
            return null;

        var textContext = retrievalContext as RetrievalTextContext;
        return new SourcePassage
        {
            SourceUrl = sources[0].URL,
            DataSourceName = retrievalContext.DataSourceName,
            Path = retrievalContext.Path,
            Category = retrievalContext.Category,
            Type = retrievalContext.Type,
            Text = matchedText,
            SurroundingContent = surroundingContent,
            PageNumber = textContext?.PageNumber is > 0 ? textContext.PageNumber : null,
            EndPageNumber = textContext?.EndPageNumber is > 0 ? textContext.EndPageNumber : null,
            SheetName = textContext?.SheetName is { Length: > 0 } sheetName ? sheetName : null,
            StartRow = textContext?.StartRow is > 0 ? textContext.StartRow : null,
            EndRow = textContext?.EndRow is > 0 ? textContext.EndRow : null,
            StartLine = textContext?.StartLine is > 0 ? textContext.StartLine : null,
            EndLine = textContext?.EndLine is > 0 ? textContext.EndLine : null,
            ChunkNumber = textContext?.ChunkNumber is > 0 ? textContext.ChunkNumber : null,
        };
    }

    /// <summary>
    /// The sources a retrieval context lends to an answer, as they are listed below it.
    /// </summary>
    /// <remarks>
    /// The reference comes first: the title and link of the passage itself where the data source
    /// names them, e.g., a local file with its page, and otherwise the data source and the path.
    /// The further links of the context follow. Only what can be opened becomes a link, i.e., a
    /// web address or a file with an absolute path. A relative path would point elsewhere depending
    /// on where it is opened from.<br/><br/>
    ///
    /// A context which names nothing to open at all, e.g., by a path which only its ERI server knows,
    /// still lends one source: the AI received it, and its passage needs a place to be read at. That
    /// source is named as text, see SourceExtensions.CreateRetrievalSourceUrl.
    /// </remarks>
    /// <param name="retrievalContext">The retrieval context.</param>
    /// <returns>The sources; none only for a context which names neither a document nor its data source.</returns>
    public static IReadOnlyList<Source> ToSources(this IRetrievalContext retrievalContext)
    {
        var sources = new List<Source>();
        var referenceTitle = GetReferenceTitle(retrievalContext);
        AddSource(sources, referenceTitle, GetReferenceLink(retrievalContext));
        foreach (var link in retrievalContext.Links)
            AddSource(sources, retrievalContext.DataSourceName, link);

        if (sources.Count is 0 && !string.IsNullOrWhiteSpace(referenceTitle))
            sources.Add(new Source(referenceTitle, SourceExtensions.CreateRetrievalSourceUrl(referenceTitle, retrievalContext.Path), SourceOrigin.RAG));

        return sources;
    }

    private static void AddSource(ICollection<Source> sources, string title, string link)
    {
        if (string.IsNullOrWhiteSpace(title) || !TryNormalizeSourceLink(link, out var normalizedLink))
            return;

        sources.Add(new Source(title, normalizedLink, SourceOrigin.RAG));
    }

    private static string GetReferenceTitle(IRetrievalContext retrievalContext) =>
        retrievalContext is RetrievalTextContext { ReferenceTitle: { Length: > 0 } referenceTitle }
            ? referenceTitle
            : retrievalContext.DataSourceName;

    private static string GetReferenceLink(IRetrievalContext retrievalContext) =>
        retrievalContext is RetrievalTextContext { ReferenceLink: { Length: > 0 } referenceLink }
            ? referenceLink
            : retrievalContext.Path;

    private static bool TryNormalizeSourceLink(string link, out string normalizedLink)
    {
        normalizedLink = string.Empty;
        if (string.IsNullOrWhiteSpace(link))
            return false;

        if (Uri.TryCreate(link, UriKind.Absolute, out var absoluteUri) && IsSupportedSourceUri(absoluteUri))
        {
            normalizedLink = absoluteUri.AbsoluteUri;
            return true;
        }

        try
        {
            if (!Path.IsPathRooted(link))
                return false;

            normalizedLink = new Uri(Path.GetFullPath(link)).AbsoluteUri;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSupportedSourceUri(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase);
}