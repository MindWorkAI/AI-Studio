namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Text sub-content for multimodal messages.
/// </summary>
public record SubContentText(SubContentType Type, string Text) : ISubContent
{
    public SubContentText() : this(SubContentType.TEXT, string.Empty)
    {
    }
}