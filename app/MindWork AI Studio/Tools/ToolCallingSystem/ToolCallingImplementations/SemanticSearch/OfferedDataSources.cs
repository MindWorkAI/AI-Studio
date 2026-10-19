using AIStudio.Settings;

namespace AIStudio.Tools.ToolCallingSystem.ToolCallingImplementations.SemanticSearch;

/// <summary>
/// The data sources Semantic Search offers the model in a request.
/// </summary>
/// <param name="Searchable">The data sources the model may search, in the order they are offered.</param>
/// <param name="Unreachable">The ERI data sources of the chat whose server could not be reached. The model learns their names, but can never search them.</param>
internal readonly record struct OfferedDataSources(IReadOnlyList<IDataSource> Searchable, IReadOnlyList<IDataSource> Unreachable)
{
    public static readonly OfferedDataSources NONE = new([], []);

    /// <summary>
    /// Whether there is nothing at all to tell the model about.
    /// </summary>
    public bool IsEmpty => this.Searchable.Count == 0 && this.Unreachable.Count == 0;
}