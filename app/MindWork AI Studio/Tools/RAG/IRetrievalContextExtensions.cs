using System.Text;

using AIStudio.Chat;

namespace AIStudio.Tools.RAG;

public static class IRetrievalContextExtensions
{
    private static readonly ILogger<IRetrievalContext> LOGGER = Program.SERVICE_PROVIDER.GetService<ILogger<IRetrievalContext>>()!;
    
    public static async Task<string> AsMarkdown(this IReadOnlyList<IRetrievalContext> retrievalContexts, StringBuilder? sb = null, CancellationToken token = default)
    {
        sb ??= new StringBuilder();
        var index = 0;
        
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
        switch (index)
        {
            case > 0 when numTotalRetrievalContexts is -1:
                sb.AppendLine($"# Retrieval context {index}");
                break;
            
            case > 0 when numTotalRetrievalContexts > 0:
                sb.AppendLine($"# Retrieval context {index} of {numTotalRetrievalContexts}");
                break;

            default:
                sb.AppendLine("# Retrieval context");
                break;
        }
        
        sb.AppendLine($"Data source name: {retrievalContext.DataSourceName}");
        sb.AppendLine($"Content category: {retrievalContext.Category}");
        sb.AppendLine($"Content type: {retrievalContext.Type}");
        sb.AppendLine($"Content path: {retrievalContext.Path}");
            
        if(retrievalContext.Links.Count > 0)
        {
            sb.AppendLine("Additional links:");
            foreach(var link in retrievalContext.Links)
                sb.AppendLine($"- {link}");
        }
            
        switch(retrievalContext)
        {
            case RetrievalTextContext textContext:
                sb.AppendLine();
                sb.AppendLine("Matched text content:");
                sb.AppendLine("````");
                sb.AppendLine(textContext.MatchedText);
                sb.AppendLine("````");
                    
                if(textContext.SurroundingContent.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Surrounding text content:");
                    foreach(var surrounding in textContext.SurroundingContent)
                    {
                        sb.AppendLine();
                        sb.AppendLine("````");
                        sb.AppendLine(surrounding);
                        sb.AppendLine("````");
                    }
                }
                    
                    
                break;
                
            case RetrievalImageContext imageContext:
                sb.AppendLine();
                sb.AppendLine("Matched image content as base64-encoded data:");
                sb.AppendLine("````");
                sb.AppendLine(await imageContext.AsBase64(token));
                sb.AppendLine("````");
                break;
                
            default:
                LOGGER.LogWarning($"The retrieval content type '{retrievalContext.Type}' of data source '{retrievalContext.DataSourceName}' at location '{retrievalContext.Path}' is not supported yet.");
                break;
        }
            
        sb.AppendLine();
        return sb.ToString();
    }
}