namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Image sub-content for multimodal messages.
/// </summary>
public record SubContentImageUrl(SubContentType Type, string ImageUrl) : ISubContent
{
    public SubContentImageUrl() : this(SubContentType.IMAGE_URL, string.Empty)
    {
    }
}