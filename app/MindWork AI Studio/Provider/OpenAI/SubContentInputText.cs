namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Text input sub-content for multimodal messages.
/// </summary>
/// <remarks>
/// Right now, this is used only by OpenAI in its responses API.
/// </remarks>
public record SubContentInputText(SubContentType Type, string Text) : ISubContent
{
    public SubContentInputText() : this(SubContentType.INPUT_TEXT, string.Empty)
    {
    }
}