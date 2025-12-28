namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Text sub-content for multimodal messages.
/// </summary>
public record SubContentText(ContentType Type, string Text) : ISubContent
{
    public SubContentText() : this(ContentType.TEXT, string.Empty)
    {
    }
}