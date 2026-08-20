using System.Text;

using AIStudio.Chat;
using AIStudio.Tools.Security;

namespace AIStudio.Tools.RAG;

public static class IRetrievalContextExtensions
{
    private static readonly ILogger<IRetrievalContext> LOGGER = Program.LOGGER_FACTORY.CreateLogger<IRetrievalContext>();
    
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
        
        contextBuilder.AppendLine($"Data source name: {retrievalContext.DataSourceName}");
        contextBuilder.AppendLine($"Content category: {retrievalContext.Category}");
        contextBuilder.AppendLine($"Content type: {retrievalContext.Type}");
        contextBuilder.AppendLine($"Content path: {retrievalContext.Path}");
            
        if(retrievalContext.Links.Count > 0)
        {
            contextBuilder.AppendLine("Additional links:");
            foreach(var link in retrievalContext.Links)
                contextBuilder.AppendLine($"- {link}");
        }

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
}