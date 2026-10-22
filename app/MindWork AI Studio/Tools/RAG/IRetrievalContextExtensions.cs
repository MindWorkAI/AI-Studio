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
    /// runtime through the index into the context. A slide or a sheet has no page, and then
    /// nothing is claimed rather than something made up.
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

        if(retrievalContext is RetrievalTextContext { PageNumber: > 0 } locatedContext)
            contextBuilder.AppendLine($"Content location: page {locatedContext.PageNumber}");

        if(retrievalContext.Links.Count is 0)
            return;

        contextBuilder.AppendLine("Additional links:");
        foreach(var link in retrievalContext.Links)
            contextBuilder.AppendLine($"- {link}");
    }

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
        // The user finds a passage under the source it belongs to. A context without a source, such
        // as a file known by a relative path only, is listed nowhere, so its passage could never be
        // shown and would only take up room in the chat file:
        //
        if (sources.Count is 0)
            return null;

        return new SourcePassage
        {
            SourceUrl = sources[0].URL,
            DataSourceName = retrievalContext.DataSourceName,
            Path = retrievalContext.Path,
            Category = retrievalContext.Category,
            Type = retrievalContext.Type,
            Text = matchedText,
            SurroundingContent = surroundingContent,
            PageNumber = retrievalContext is RetrievalTextContext { PageNumber: > 0 } locatedContext ? locatedContext.PageNumber : null,
        };
    }

    /// <summary>
    /// The sources a retrieval context lends to an answer, as they are listed below it.
    /// </summary>
    /// <remarks>
    /// The reference comes first: the title and link of the passage itself where the data source
    /// names them, e.g., a local file with its page, and otherwise the data source and the path.
    /// The further links of the context follow. Only what can be opened becomes a source, i.e., a
    /// web address or a file with an absolute path. A relative path would point elsewhere depending
    /// on where it is opened from.
    /// </remarks>
    /// <param name="retrievalContext">The retrieval context.</param>
    /// <returns>The sources, which may be none.</returns>
    public static IReadOnlyList<Source> ToSources(this IRetrievalContext retrievalContext)
    {
        var sources = new List<Source>();
        AddSource(sources, GetReferenceTitle(retrievalContext), GetReferenceLink(retrievalContext));
        foreach (var link in retrievalContext.Links)
            AddSource(sources, retrievalContext.DataSourceName, link);

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