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
    //
    // The properties of the parameters, declared again to be settable. The source generation of System.Text.Json
    // sets every init-only property while it creates the object, also one missing in the JSON, so its value from
    // the parameterless constructor would get lost. Keep them first and in the order of the parameters, which is
    // the order they get written in:
    //
    public SubContentType Type { get; set; } = Type;
    public ISubContentImageUrl ImageUrl { get; set; } = ImageUrl;

    public SubContentImageUrlNested() : this(SubContentType.IMAGE_URL, new SubContentImageUrlData())
    {
    }
}
