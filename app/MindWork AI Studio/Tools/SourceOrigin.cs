namespace AIStudio.Tools;

/// <summary>
/// Represents the origin of a source.
/// </summary>
public enum SourceOrigin
{
    /// <summary>
    /// The LLM provided the source.
    /// </summary>
    LLM,
    
    /// <summary>
    /// The source was provided by the RAG process.
    /// </summary>
    RAG,

    /// <summary>
    /// The source was used by a locally executed tool.
    /// </summary>
    TOOL,
}
