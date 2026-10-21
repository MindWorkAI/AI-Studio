using System.Text;

using AIStudio.Settings;

namespace AIStudio.Tools.RAG;

/// <summary>
/// Tells the model which data sources of the chat could not be searched because their server could not be reached.
/// </summary>
/// <remarks>
/// Semantic Search writes this into the description of its tool, the classic RAG process next to
/// the passages it found. Both use the same words, so that the model hears the same in either way.
/// Only external data sources have a server which may be out of reach, hence their kind is not
/// listed.<br/><br/>
/// The note holds nothing but the names the data sources have in AI Studio, configured by the user
/// or the organization, and no word of their servers. It therefore needs no prompt-injection filter,
/// like the rest of the description of the tool.
/// </remarks>
public static class UnreachableDataSources
{
    /// <summary>
    /// Writes the note on the given data sources.
    /// </summary>
    /// <param name="builder">The builder to write into.</param>
    /// <param name="dataSources">The data sources whose server could not be reached, in the order to list them.</param>
    public static void AppendTo(StringBuilder builder, IReadOnlyList<IDataSource> dataSources)
    {
        builder.AppendLine("These external data sources of this chat could not be reached or signed in to right now, so they cannot be searched. Their content is missing rather than empty; tell the user so when a question concerns them:");
        foreach (var dataSource in dataSources)
            builder.AppendLine($"- name='{dataSource.Name}'");
    }
}