namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Contract for sub-content in multimodal messages.
/// </summary>
public interface ISubContent
{
    /// <summary>
    /// The type of the sub-content.
    /// </summary>
    public ContentType Type { get; init; }
}