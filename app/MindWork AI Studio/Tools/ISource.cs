namespace AIStudio.Tools;

/// <summary>
/// Data model for a source used in the response.
/// </summary>
public interface ISource
{
    /// <summary>
    /// The title of the source.
    /// </summary>
    public string Title { get; }
    
    /// <summary>
    /// The URL of the source.
    /// </summary>
    public string URL { get; }
    
    /// <summary>
    /// The origin of the source, whether it was provided by the AI or by the RAG process.
    /// </summary>
    public SourceOrigin Origin { get; }
}