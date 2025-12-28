namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Image sub-content for multimodal messages.
/// </summary>
public record SubContentImageUrl(ContentType Type, string ImageUrl) : ISubContent
{
    public SubContentImageUrl() : this(ContentType.IMAGE_URL, string.Empty)
    {
    }
}