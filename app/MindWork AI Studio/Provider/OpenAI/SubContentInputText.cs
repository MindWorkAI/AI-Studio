namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Text input sub-content for multimodal messages.
/// </summary>
/// <remarks>
/// Right now, this is used only by OpenAI in its responses API.
/// </remarks>
public record SubContentInputText(SubContentType Type, string Text) : ISubContent
{
    //
    // The properties of the parameters, declared again to be settable. The source generation of System.Text.Json
    // sets every init-only property while it creates the object, also one missing in the JSON, so its value from
    // the parameterless constructor would get lost. Keep them first and in the order of the parameters, which is
    // the order they get written in:
    //
    public SubContentType Type { get; set; } = Type;
    public string Text { get; set; } = Text;

    public SubContentInputText() : this(SubContentType.INPUT_TEXT, string.Empty)
    {
    }
}