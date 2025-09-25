namespace AIStudio.Tools;

/// <summary>
/// Represents the origin of a source, whether it was provided by the LLM or by the RAG process.
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
}