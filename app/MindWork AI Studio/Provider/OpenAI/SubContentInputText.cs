namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Text input sub-content for multimodal messages.
/// </summary>
/// <remarks>
/// Right now, this is used only by OpenAI in its responses API.
/// </remarks>
public record SubContentInputText(ContentType Type, string Text) : ISubContent
{
    public SubContentInputText() : this(ContentType.INPUT_TEXT, string.Empty)
    {
    }
}