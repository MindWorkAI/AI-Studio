namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Image input sub-content for multimodal messages.
/// </summary>
/// <remarks>
/// Right now, this is used only by OpenAI in its responses API.
/// </remarks>
public record SubContentInputImage(SubContentType Type, string ImageUrl) : ISubContent
{
    //
    // The properties of the parameters, declared again to be settable. The source generation of System.Text.Json
    // sets every init-only property while it creates the object, also one missing in the JSON, so its value from
    // the parameterless constructor would get lost. Keep them first and in the order of the parameters, which is
    // the order they get written in:
    //
    public SubContentType Type { get; set; } = Type;
    public string ImageUrl { get; set; } = ImageUrl;

    public SubContentInputImage() : this(SubContentType.INPUT_IMAGE, string.Empty)
    {
    }
}