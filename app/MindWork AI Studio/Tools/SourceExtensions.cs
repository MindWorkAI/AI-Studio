using System.Text;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class SourceExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SourceExtensions).Namespace, nameof(SourceExtensions));
    
    /// <summary>
    /// Converts a list of sources to a markdown-formatted string.
    /// </summary>
    /// <param name="sources">The list of sources to convert.</param>
    /// <returns>A markdown-formatted string representing the sources.</returns>
    public static string ToMarkdown(this IList<Source> sources)
    {
        var sb = new StringBuilder();
        var ragSources = new List<ISource>();
        var sourceNum = 0;
        var addedLLMHeaders = false;
        foreach (var source in sources)
        {
            switch (source.Origin)
            {
                case SourceOrigin.RAG: 
                    ragSources.Add(source);
                    break;
                
                case SourceOrigin.LLM:
                    if (!addedLLMHeaders)
                    {
                        sb.Append("## ");
                        sb.AppendLine(TB("Sources provided by the AI"));
                        addedLLMHeaders = true;
                    }
                    
                    sb.Append($"- [{++sourceNum}] ");
                    sb.Append('[');
                    sb.Append(source.Title);
                    sb.Append("](");
                    sb.Append(source.URL);
                    sb.AppendLine(")");
                    break;
            }
        }
        
        if(ragSources.Count == 0)
            return sb.ToString();
        
        sb.AppendLine();
        sb.Append("## ");
        sb.AppendLine(TB("Sources provided by the data providers"));
        
        foreach (var source in ragSources)
        {
            sb.Append($"- [{++sourceNum}] ");
            sb.Append('[');
            sb.Append(source.Title);
            sb.Append("](");
            sb.Append(source.URL);
            sb.AppendLine(")");
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Merges a list of added sources into an existing list of sources, avoiding duplicates based on URL and Title.
    /// </summary>
    /// <param name="sources">The existing list of sources to merge into.</param>
    /// <param name="addedSources">The list of sources to add.</param>
    public static void MergeSources(this IList<Source> sources, IList<ISource> addedSources)
    {
        foreach (var addedSource in addedSources)
            if (sources.All(s => s.URL != addedSource.URL && s.Title != addedSource.Title))
                sources.Add((Source)addedSource);
    }
}