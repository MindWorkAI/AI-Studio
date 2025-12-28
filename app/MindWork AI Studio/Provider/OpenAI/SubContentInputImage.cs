namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Image input sub-content for multimodal messages.
/// </summary>
/// <remarks>
/// Right now, this is used only by OpenAI in its responses API.
/// </remarks>
public record SubContentInputImage(ContentType Type, string ImageUrl) : ISubContent
{
    public SubContentInputImage() : this(ContentType.INPUT_IMAGE, string.Empty)
    {
    }
}