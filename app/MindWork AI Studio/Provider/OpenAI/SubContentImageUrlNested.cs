namespace AIStudio.Provider.OpenAI;

/// <summary>
/// Image sub-content for multimodal messages using nested URL format.
/// </summary>
/// <remarks>
/// This record is used when the provider expects the format:
/// <code>
/// { "type": "image_url", "image_url": { "url": "data:image/jpeg;base64,..." } }
/// </code>
/// Used by LM Studio, VLLM, and other OpenAI-compatible providers.
/// </remarks>
public record SubContentImageUrlNested(SubContentType Type, ISubContentImageUrl ImageUrl) : ISubContent
{
    public SubContentImageUrlNested() : this(SubContentType.IMAGE_URL, new SubContentImageUrlData())
    {
    }
}
