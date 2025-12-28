namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Image input sub-content for multimodal messages.
/// </summary>
/// <remarks>
/// Right now, this is used only by OpenAI in its responses API.
/// </remarks>
public record SubContentInputImage(SubContentType Type, string ImageUrl) : ISubContent
{
    public SubContentInputImage() : this(SubContentType.INPUT_IMAGE, string.Empty)
    {
    }
}