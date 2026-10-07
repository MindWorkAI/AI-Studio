using System.Text;

using AIStudio.Chat;
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

    public static async Task<string> AsMarkdown(this IReadOnlyList<IRetrievalContext> retrievalContexts, StringBuilder? sb = null, CancellationToken token = default)
    {
        sb ??= new StringBuilder();
        var index = 0;
        
        //
        // One report for the whole retrieval run: a query may pull in dozens of contexts, and
        // the user wants to know that something was filtered, not to acknowledge it per context.
        //
        var guardService = Program.SERVICE_PROVIDER.GetRequiredService<PromptInjectionGuardService>();
        await using var reportingScope = guardService.BeginAction();

        foreach(var retrievalContext in retrievalContexts)
        {
            index++;
            await retrievalContext.AsMarkdown(sb, index, retrievalContexts.Count, token);
        }

        return sb.ToString();
    }
    
    public static async Task<string> AsMarkdown(this IRetrievalContext retrievalContext, StringBuilder? sb = null, int index = -1, int numTotalRetrievalContexts = -1, CancellationToken token = default)
    {
        sb ??= new StringBuilder();
        var contextBuilder = new StringBuilder();
        switch (index)
        {
            case > 0 when numTotalRetrievalContexts is -1:
                contextBuilder.AppendLine($"# Retrieval context {index}");
                break;
            
            case > 0 when numTotalRetrievalContexts > 0:
                contextBuilder.AppendLine($"# Retrieval context {index} of {numTotalRetrievalContexts}");
                break;

            default:
                contextBuilder.AppendLine("# Retrieval context");
                break;
        }
        
        AppendContextDescription(contextBuilder, retrievalContext);

        var guardService = Program.SERVICE_PROVIDER.GetRequiredService<PromptInjectionGuardService>();
        var source = PromptInjectionSource.RetrievalContext(retrievalContext.DataSourceName, retrievalContext.Path);

        switch(retrievalContext)
        {
            case RetrievalTextContext textContext:
                contextBuilder.AppendLine();
                contextBuilder.AppendLine("Matched text content:");
                contextBuilder.AppendLine("````");
                contextBuilder.AppendLine(textContext.MatchedText);
                contextBuilder.AppendLine("````");

                if(textContext.SurroundingContent.Count > 0)
                {
                    contextBuilder.AppendLine();
                    contextBuilder.AppendLine("Surrounding text content:");
                    foreach(var surrounding in textContext.SurroundingContent)
                    {
                        contextBuilder.AppendLine();
                        contextBuilder.AppendLine("````");
                        contextBuilder.AppendLine(surrounding);
                        contextBuilder.AppendLine("````");
                    }
                }

                await FilterWhatWeHaveSoFar();
                break;

            case RetrievalImageContext imageContext:
                //
                // Filtering happens before the image is appended, and only covers the text
                // around it. Base64 image data is not prose, and running it through the filter
                // would have it treated as one enormous encoded carrier.
                //
                await FilterWhatWeHaveSoFar();
                contextBuilder.AppendLine();
                contextBuilder.AppendLine("Matched image content as base64-encoded data:");
                contextBuilder.AppendLine("````");
                contextBuilder.AppendLine(await imageContext.TryAsBase64(token) is (success: true, { } base64Image)
                        ? base64Image
                        : string.Empty);
                contextBuilder.AppendLine("````");
                break;

            default:
                await FilterWhatWeHaveSoFar();
                LOGGER.LogWarning($"The retrieval content type '{retrievalContext.Type}' of data source '{retrievalContext.DataSourceName}' at location '{retrievalContext.Path}' is not supported yet.");
                break;
        }
            
        contextBuilder.AppendLine();
        sb.Append(contextBuilder);
        return sb.ToString();

        //
        // Replaces what has been built so far with its filtered version. A data source is as
        // untrusted as any other external content: it may serve text written to steer the model
        // rather than to answer the query.
        //
        async Task FilterWhatWeHaveSoFar()
        {
            var sanitized = await guardService.SanitizeAsync(contextBuilder.ToString(), source);
            contextBuilder.Clear();
            contextBuilder.Append(sanitized);
        }
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