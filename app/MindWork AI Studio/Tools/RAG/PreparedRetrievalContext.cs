using System.Text;

namespace AIStudio.Tools.RAG;

/// <summary>
/// A retrieval context as the AI receives it: filtered for prompt injections, written out, and
/// ready to be numbered.
/// </summary>
/// <remarks>
/// Made by IRetrievalContextExtensions.PrepareForModelAsync. Which number a context gets depends on
/// which contexts the caller hands on in the end, so the header is written only then, see
/// ToMarkdown. The app writes it itself, which is why it needs no filter.
/// </remarks>
/// <param name="Context">The context as the data source delivered it.</param>
/// <param name="Body">What the AI is told about the context below its header: the filtered description, then the filtered content.</param>
/// <param name="Sources">The sources the context lends to the answer, which may be none.</param>
/// <param name="Passage">What the user is shown of the context, or null when the context has no source to list it under.</param>
public sealed record PreparedRetrievalContext(IRetrievalContext Context, string Body, IReadOnlyList<Source> Sources, SourcePassage? Passage)
{
    /// <summary>
    /// Writes what the AI is told about the context, headed by its number.
    /// </summary>
    /// <param name="index">The number of the context among all of them, or null when it stands alone.</param>
    /// <param name="numTotalRetrievalContexts">How many contexts there are, or null when that is unknown.</param>
    /// <returns>The context as Markdown, ending with an empty line to set it apart from the next one.</returns>
    public string ToMarkdown(int? index = null, int? numTotalRetrievalContexts = null)
    {
        var sb = new StringBuilder();
        switch (index)
        {
            case > 0 when numTotalRetrievalContexts is null:
                sb.AppendLine($"# Retrieval context {index}");
                break;

            case > 0 when numTotalRetrievalContexts > 0:
                sb.AppendLine($"# Retrieval context {index} of {numTotalRetrievalContexts}");
                break;

            default:
                sb.AppendLine("# Retrieval context");
                break;
        }

        sb.Append(this.Body);
        sb.AppendLine();
        return sb.ToString();
    }
}